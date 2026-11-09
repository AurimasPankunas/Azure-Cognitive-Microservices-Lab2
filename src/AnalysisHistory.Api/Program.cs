using AnalysisHistory.Api.Consumers;
using AnalysisHistory.Api.Data;
using DotNetEnv;
using Microsoft.EntityFrameworkCore;

// Load the repository-level .env file when running locally.
// Docker Compose supplies the same values as environment variables instead.
Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Map the simple .env names to ASP.NET Core configuration keys.
var rabbitUser = Environment.GetEnvironmentVariable("RABBITMQ_USER");
if (!string.IsNullOrWhiteSpace(rabbitUser))
    builder.Configuration["RabbitMq:User"] = rabbitUser;

var rabbitPassword = Environment.GetEnvironmentVariable("RABBITMQ_PASS");
if (!string.IsNullOrWhiteSpace(rabbitPassword))
    builder.Configuration["RabbitMq:Password"] = rabbitPassword;

// Register MVC controllers so requests can reach HistoryController.
builder.Services.AddControllers();

// Register Swagger/OpenAPI for interactive API testing.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Use the Docker-provided connection string or a local SQLite file.
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=history.db";

builder.Services.AddDbContext<AnalysisDbContext>(options =>
    options.UseSqlite(connectionString));

// The consumer runs for the whole application lifetime and listens to RabbitMQ.
builder.Services.AddHostedService<RabbitMqConsumerService>();

var app = builder.Build();

// Create the SQLite schema on startup for this lab (migrations are not required).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AnalysisDbContext>();
    db.Database.EnsureCreated();
}

// Swagger is enabled unconditionally to simplify the Docker demo.
app.UseSwagger();
app.UseSwaggerUI();

// Simple health endpoint for Docker Compose and manual checks.
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "analysis-history-api" }));

// Map controller routes such as GET /api/history/all.
app.MapControllers();

app.Run();
