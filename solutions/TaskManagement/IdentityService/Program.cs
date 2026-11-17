using System.Text;
using log4net;
using log4net.Config;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TaskManagement.IdentityService.Data;
using TaskManagement.IdentityService.Models;
using TaskManagement.IdentityService.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

// ------------------------------- log4net -----------------------------------
var logRepository = LogManager.GetRepository(System.Reflection.Assembly.GetEntryAssembly()!);
XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));

var logger = LogManager.GetLogger(typeof(Program));
logger.Info("Запуск IdentityService");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "IdentityService API",
        Version = "v1",
        Description = "Микросервис авторизации и справочника пользователей ИС «Управление задачами проекта»"
    });
});

// log4net ILog как зависимость контроллеров
builder.Services.AddSingleton(typeof(ILog), LogManager.GetLogger(typeof(Program)));

// ------------------------------- база данных -------------------------------
builder.Services.AddDbContext<IdentityDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "IdentityService API v1"));
}

app.MapControllers();

// --------------------------- первичное наполнение --------------------------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    db.Database.EnsureCreated();

    if (!db.Users.Any())
    {
        logger.Info("Наполнение справочника пользователей");

        foreach (var (login, password, fullName, email, role) in new[]
        {
            ("manager", builder.Configuration["SeedUsers:manager:Password"] ?? throw new InvalidOperationException("Задайте пароль SeedUsers:manager:Password"), "Смирнова Елена Петровна", "smirnova@corp.ru", "Руководитель проекта"),
            ("executor", builder.Configuration["SeedUsers:executor:Password"] ?? throw new InvalidOperationException("Задайте пароль SeedUsers:executor:Password"), "Иванов Алексей Александрович", "ivanov@corp.ru", "Исполнитель"),
            ("petrov", builder.Configuration["SeedUsers:petrov:Password"] ?? throw new InvalidOperationException("Задайте пароль SeedUsers:petrov:Password"), "Петров Виктор Сергеевич", "petrov@corp.ru", "Исполнитель")
        })
        {
            var salt = PasswordHasher.CreateSalt();
            db.Users.Add(new IdentityUser
            {
                Login = login,
                PasswordSalt = salt,
                PasswordHash = PasswordHasher.Hash(password, salt),
                FullName = fullName,
                Email = email,
                Role = role
            });
        }

        db.SaveChanges();
        logger.Info($"Добавлено {db.Users.Count()} пользователей");
    }
}

logger.Info("IdentityService успешно запущен");

app.Run();
