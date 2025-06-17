using Serilog;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/api-gateway-.txt", rollingInterval: RollingInterval.Day)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();

// Add Ocelot configuration file
builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { 
        Title = "API Gateway", 
        Version = "v1",
        Description = "API Gateway for microservices"
    });
});

// Add Ocelot services
builder.Services.AddOcelot(builder.Configuration);

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy",
        builder => builder
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI();

app.UseSerilogRequestLogging();

app.UseCors("CorsPolicy");

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Starting API Gateway");

// Health check endpoint
app.MapGet("/health", () =>
{
    logger.LogInformation("Health check requested");
    return Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow });
})
.WithName("HealthCheck")
.WithDescription("Returns the health status of the API Gateway")
.WithTags("Health");

// Add middleware to log request routing
app.Use(async (context, next) =>
{
    var originalPath = context.Request.Path;
    var method = context.Request.Method;

    logger.LogInformation("Request received: {Method} {Path}", method, originalPath);

    await next();

    logger.LogInformation("Response sent for: {Method} {Path}, Status: {StatusCode}", 
        method, originalPath, context.Response.StatusCode);
});

// Configure Ocelot middleware
await app.UseOcelot();

app.Run();
