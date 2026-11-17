using System.Text;
using log4net;
using log4net.Config;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

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
logger.Info("Запуск ReportService");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "ReportService API",
        Version = "v1",
        Description = "Композитный микросервис отчётов ИС «Управление задачами проекта»"
    });
});

builder.Services.AddSingleton(typeof(ILog), LogManager.GetLogger(typeof(Program)));

// ---------------------- HTTP-клиенты к микросервисам -----------------------
builder.Services.AddHttpClient("TaskService", client =>
    client.BaseAddress = new Uri(builder.Configuration["TaskServiceUrl"] ?? "https://localhost:5002"));

builder.Services.AddHttpClient("IdentityService", client =>
    client.BaseAddress = new Uri(builder.Configuration["IdentityServiceUrl"] ?? "https://localhost:5001"));

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
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "ReportService API v1"));
}

app.MapControllers();

logger.Info("ReportService успешно запущен");

app.Run();
