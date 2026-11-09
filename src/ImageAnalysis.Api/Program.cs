using DotNetEnv;
using ImageAnalysis.Api.Services;

// Load the repository-level .env file when running locally.
// Docker Compose supplies the same values as environment variables instead.
Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Map the simple .env names to ASP.NET Core configuration keys.
// In Docker, Compose sets Azure__Endpoint / Azure__Key directly.
var azureEndpoint = Environment.GetEnvironmentVariable("AZURE_VISION_ENDPOINT");
if (!string.IsNullOrWhiteSpace(azureEndpoint))
    builder.Configuration["Azure:Endpoint"] = azureEndpoint;

var azureKey = Environment.GetEnvironmentVariable("AZURE_VISION_KEY");
if (!string.IsNullOrWhiteSpace(azureKey))
    builder.Configuration["Azure:Key"] = azureKey;

var rabbitUser = Environment.GetEnvironmentVariable("RABBITMQ_USER");
if (!string.IsNullOrWhiteSpace(rabbitUser))
    builder.Configuration["RabbitMq:User"] = rabbitUser;

var rabbitPassword = Environment.GetEnvironmentVariable("RABBITMQ_PASS");
if (!string.IsNullOrWhiteSpace(rabbitPassword))
    builder.Configuration["RabbitMq:Password"] = rabbitPassword;

// Register MVC controllers so requests can reach AnalysisController.
builder.Services.AddControllers();

// Register Swagger/OpenAPI so the API can be tested from a browser.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// AzureVisionService is stateless apart from the Azure client, so a singleton is enough.
builder.Services.AddSingleton<AzureVisionService>();

// RabbitMqPublisher keeps a long-lived RabbitMQ connection, so it is also a singleton.
builder.Services.AddSingleton<RabbitMqPublisher>();

var app = builder.Build();

// Swagger is enabled unconditionally to simplify the lab demo inside Docker.
app.UseSwagger();
app.UseSwaggerUI();

// Simple health endpoint, useful for Docker Compose and manual checks.
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "image-analysis-api" }));

// Map controller routes such as POST /api/analysis/upload.
app.MapControllers();

app.Run();
