using log4net;
using log4net.Config;

var builder = WebApplication.CreateBuilder(args);

// ------------------------------- log4net -----------------------------------
Directory.CreateDirectory("logs");
var logRepository = LogManager.GetRepository(System.Reflection.Assembly.GetEntryAssembly()!);
XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));

var logger = LogManager.GetLogger(typeof(Program));
logger.Info("Запуск WebClient");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "WebClient API",
        Version = "v1",
        Description = "Клиентский интерфейс ИС «Управление задачами проекта»"
    });
});

builder.Services.AddSingleton(typeof(ILog), LogManager.GetLogger(typeof(Program)));

// ---------------------- HTTP-клиенты к микросервисам -----------------------
builder.Services.AddHttpClient("IdentityService", client =>
    client.BaseAddress = new Uri(builder.Configuration["IdentityServiceUrl"] ?? "https://localhost:5001"));

builder.Services.AddHttpClient("TaskService", client =>
    client.BaseAddress = new Uri(builder.Configuration["TaskServiceUrl"] ?? "https://localhost:5002"));

builder.Services.AddHttpClient("ReportService", client =>
    client.BaseAddress = new Uri(builder.Configuration["ReportServiceUrl"] ?? "https://localhost:5003"));

// --------------------------- Cookie-аутентификация -------------------------
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/api/auth/login";
        options.AccessDeniedPath = "/api/auth/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

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
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "WebClient API v1"));
}

app.MapControllers();

logger.Info("WebClient успешно запущен");

app.Run();
