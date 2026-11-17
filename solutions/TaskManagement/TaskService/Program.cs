using System.Text;
using log4net;
using log4net.Config;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TaskManagement.TaskService.Data;
using TaskManagement.TaskService.Middleware;
using TaskManagement.TaskService.Models;
using Task = TaskManagement.TaskService.Models.Task;
using TaskStatus = TaskManagement.TaskService.Models.TaskStatus;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

// ------------------------------- log4net -----------------------------------
Directory.CreateDirectory("logs");
var logRepository = LogManager.GetRepository(System.Reflection.Assembly.GetEntryAssembly()!);
XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));

var logger = LogManager.GetLogger(typeof(Program));
logger.Info("Запуск TaskService");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "TaskService API",
        Version = "v1",
        Description = "Микросервис задач и проектов ИС «Управление задачами проекта»"
    });
});

builder.Services.AddSingleton(typeof(ILog), LogManager.GetLogger(typeof(Program)));

// ------------------------------- база данных -------------------------------
builder.Services.AddDbContext<TaskDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ---------------------------------- JWT ------------------------------------
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Задайте Jwt:Key в appsettings.Local.json или переменных окружения");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "TaskManagement";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtIssuer,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

// протоколирование запросов: ставится после авторизации, чтобы в журнал
// попадал пользователь из JWT
app.UseMiddleware<RequestLoggingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "TaskService API v1"));
}

app.MapControllers();

// --------------------------- первичное наполнение --------------------------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TaskDbContext>();
    db.Database.EnsureCreated();
    // Совместимость с базами, созданными до добавления владельца проекта.
    await db.Database.ExecuteSqlRawAsync(
        """ALTER TABLE projects ADD COLUMN IF NOT EXISTS "ManagerId" bigint NOT NULL DEFAULT 0""");


    // Справочник статусов
    if (!db.TaskStatuses.Any())
    {
        await db.TaskStatuses.AddRangeAsync(
            new TaskStatus { Code = "NEW", Name = "Новая", Order = 1, IsFinal = false },
            new TaskStatus { Code = "ASSIGNED", Name = "Назначена", Order = 2, IsFinal = false },
            new TaskStatus { Code = "IN_PROGRESS", Name = "В работе", Order = 3, IsFinal = false },
            new TaskStatus { Code = "IN_REVIEW", Name = "На проверке", Order = 4, IsFinal = false },
            new TaskStatus { Code = "COMPLETED", Name = "Завершена", Order = 5, IsFinal = true },
            new TaskStatus { Code = "CANCELLED", Name = "Отменена", Order = 6, IsFinal = true });
        await db.SaveChangesAsync();
        logger.Info("Заполнен справочник статусов");
    }

    // Справочник «Правило перехода»
    if (!db.TransitionRules.Any())
    {
        const string Manager = "Руководитель проекта";
        const string Executor = "Исполнитель";

        await db.TransitionRules.AddRangeAsync(
            new TransitionRule { FromStatus = "Новая", ToStatus = "Назначена", UserRole = Manager, RequireComment = false },
            new TransitionRule { FromStatus = "Назначена", ToStatus = "В работе", UserRole = Executor, RequireComment = false },
            new TransitionRule { FromStatus = "В работе", ToStatus = "На проверке", UserRole = Executor, RequireComment = false },
            new TransitionRule { FromStatus = "На проверке", ToStatus = "Завершена", UserRole = Manager, RequireComment = false },
            new TransitionRule { FromStatus = "На проверке", ToStatus = "В работе", UserRole = Manager, RequireComment = true },
            new TransitionRule { FromStatus = "Новая", ToStatus = "Отменена", UserRole = Manager, RequireComment = false },
            new TransitionRule { FromStatus = "Назначена", ToStatus = "Отменена", UserRole = Manager, RequireComment = false },
            new TransitionRule { FromStatus = "В работе", ToStatus = "Отменена", UserRole = Manager, RequireComment = false },
            new TransitionRule { FromStatus = "На проверке", ToStatus = "Отменена", UserRole = Manager, RequireComment = false });
        await db.SaveChangesAsync();
        logger.Info("Заполнен справочник правил перехода");
    }

    // Справочник приоритетов
    if (!db.Priorities.Any())
    {
        await db.Priorities.AddRangeAsync(
            new Priority { Name = "Низкий", Weight = 1 },
            new Priority { Name = "Средний", Weight = 2 },
            new Priority { Name = "Высокий", Weight = 3 },
            new Priority { Name = "Критический", Weight = 4 });
        await db.SaveChangesAsync();
        logger.Info("Заполнен справочник приоритетов");
    }

    // Пользователи (совпадают по логину с IdentityService)
    if (!db.Users.Any())
    {
        await db.Users.AddRangeAsync(
            new User { Login = "manager", PasswordHash = "external", FullName = "Смирнова Елена Петровна", Email = "smirnova@corp.ru", Role = "Руководитель проекта" },
            new User { Login = "executor", PasswordHash = "external", FullName = "Иванов Алексей Александрович", Email = "ivanov@corp.ru", Role = "Исполнитель" },
            new User { Login = "petrov", PasswordHash = "external", FullName = "Петров Виктор Сергеевич", Email = "petrov@corp.ru", Role = "Исполнитель" });
        await db.SaveChangesAsync();
        logger.Info("Заполнен справочник пользователей");
    }

    // Проект и участники
    if (!db.Projects.Any())
    {
        var project = new Project
        {
            Code = "ПР-2026-04",
            Name = "Портал поддержки",
            Description = "Корпортал технической поддержки для внутренних клиентов",
            StartDate = TimeHelper.ToUtc(new DateTime(2026, 9, 1)),
            PlannedEndDate = TimeHelper.ToUtc(new DateTime(2026, 12, 15))
        };

        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var executors = await db.Users.Where(u => u.Role == "Исполнитель").ToListAsync();
        foreach (var executor in executors)
        {
            db.Participations.Add(new Participation
            {
                ProjectId = project.Id,
                ExecutorId = executor.Id,
                JoinedAt = TimeHelper.ToUtc(new DateTime(2026, 9, 1).AddDays(executor.Id))
            });
        }

        await db.SaveChangesAsync();
        logger.Info($"Создан проект {project.Id}: {project.Name}");
    }

    var managerId = await db.Users.Where(u => u.Login == "manager")
        .Select(u => u.Id).SingleAsync();
    await db.Projects.Where(p => p.ManagerId == 0)
        .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ManagerId, managerId));

    // Демонстрационные задачи
    if (!db.Tasks.Any())
    {
        var project = await db.Projects.FirstAsync();
        var executors = await db.Users.Where(u => u.Role == "Исполнитель").ToListAsync();

        var tasks = new List<Task>
        {
            new()
            {
                Number = 1, Name = "Форма авторизации", Description = "Сверстать и подключить форму входа",
                CreatedAt = TimeHelper.ToUtc(new DateTime(2026, 10, 1)), Deadline = TimeHelper.ToUtc(new DateTime(2026, 10, 25)),
                Status = "Новая", Priority = "Средний", ProjectId = project.Id, ExecutorId = null, EstimatedHours = 8
            },
            new()
            {
                Number = 2, Name = "Список проектов", Description = "Страница со списком проектов и фильтрами",
                CreatedAt = TimeHelper.ToUtc(new DateTime(2026, 10, 2)), Deadline = TimeHelper.ToUtc(new DateTime(2026, 10, 30)),
                Status = "В работе", Priority = "Высокий", ProjectId = project.Id, ExecutorId = executors[0].Id, EstimatedHours = 12
            },
            new()
            {
                Number = 3, Name = "Отчёт по статусам", Description = "Формирование отчёта по статусам задач проекта",
                CreatedAt = TimeHelper.ToUtc(new DateTime(2026, 10, 3)), Deadline = TimeHelper.ToUtc(new DateTime(2026, 10, 20)),
                Status = "На проверке", Priority = "Высокий", ProjectId = project.Id, ExecutorId = executors[1].Id, EstimatedHours = 16
            }
        };

        await db.Tasks.AddRangeAsync(tasks);
        await db.SaveChangesAsync();

        foreach (var task in tasks)
        {
            db.TaskHistories.Add(new TaskHistory
            {
                TaskId = task.Id,
                ChangedAt = task.CreatedAt,
                FromStatus = "—",
                ToStatus = task.Status,
                Comment = "Первичное состояние при наполнении",
                AuthorId = 1
            });
        }

        await db.SaveChangesAsync();
        logger.Info("Добавлены демонстрационные задачи");
    }
}

logger.Info("TaskService успешно запущен");

app.Run();
