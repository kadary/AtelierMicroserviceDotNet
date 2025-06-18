using Serilog;
using MassTransit;
using NotificationService.Consumers;
using NotificationService.Services;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/notification-service-.txt", rollingInterval: RollingInterval.Day)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();

// Create logger for startup configuration
var loggerFactory = LoggerFactory.Create(builder => builder.AddSerilog());
var logger = loggerFactory.CreateLogger<Program>();

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { 
        Title = "Notification Service API", 
        Version = "v1",
        Description = "A microservice for sending notifications based on events"
    });
});

// Register services
builder.Services.AddSingleton<IEmailService, EmailService>();

// Add Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.Authority = "http://identity-server:5004"; // IdentityServer URL
    options.RequireHttpsMetadata = false; // For development only
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateAudience = false
    };
});

// Add Authorization
builder.Services.AddAuthorization();

// Configure MassTransit with RabbitMQ - Simplified configuration
builder.Services.AddMassTransit(x =>
{
    // Add consumer
    x.AddConsumer<OrderCreatedConsumer>();

    logger.LogInformation("Configured MassTransit with OrderCreatedConsumer");

    x.UsingRabbitMq((context, cfg) =>
    {
        // Configure RabbitMQ connection
        cfg.Host("rabbitmq", "/", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });

        // Configure consumer endpoint
        cfg.ReceiveEndpoint("order-created", e =>
        {
            logger.LogInformation("Configuring OrderCreated consumer endpoint");

            // Configure consumer
            e.ConfigureConsumer<OrderCreatedConsumer>(context);
            logger.LogInformation("Configured OrderCreatedConsumer");

            // Configure retry policy
            e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
            logger.LogInformation("Configured message retry policy");
        });
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

app.UseSerilogRequestLogging();

// Add authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Use the existing logger
logger.LogInformation("Starting NotificationService");

// Notification endpoints
var notificationsGroup = app.MapGroup("/api/notifications")
    .WithTags("Notifications")
    .WithOpenApi()
    .RequireAuthorization(); // Require authorization for all endpoints in this group

// Get notification status
notificationsGroup.MapGet("/status", () =>
{
    logger.LogInformation("Notification status requested");
    return Results.Ok(new { 
        Status = "Active", 
        Consumers = new[] { "OrderCreatedConsumer" },
        Timestamp = DateTime.UtcNow 
    });
})
.WithName("GetNotificationStatus")
.WithDescription("Gets the status of the notification service")
.Produces<object>(StatusCodes.Status200OK);

// Test send email endpoint (for demonstration purposes)
notificationsGroup.MapPost("/test-email", async (TestEmailRequest request, IEmailService emailService) =>
{
    logger.LogInformation("Test email requested to {Email}", request.Email);

    try
    {
        var result = await emailService.SendOrderConfirmationAsync(
            request.Email,
            "Test Email - Order Confirmation",
            request.Name,
            Guid.NewGuid(),
            99.99m,
            3);

        if (result)
        {
            logger.LogInformation("Test email sent successfully to {Email}", request.Email);
            return Results.Ok(new { Success = true, Message = "Test email sent successfully" });
        }
        else
        {
            logger.LogWarning("Failed to send test email to {Email}", request.Email);
            return Results.BadRequest(new { Success = false, Message = "Failed to send test email" });
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error sending test email to {Email}", request.Email);
        return Results.Problem("An error occurred while sending the test email.");
    }
})
.WithName("SendTestEmail")
.WithDescription("Sends a test email (for demonstration purposes)")
.Produces<object>(StatusCodes.Status200OK)
.Produces<object>(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status500InternalServerError);

// Health check endpoint
app.MapGet("/health", () =>
{
    logger.LogInformation("Health check requested");
    return Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow });
})
.WithName("HealthCheck")
.WithDescription("Returns the health status of the service")
.WithTags("Health");

app.Run();

// Request model for test email
public class TestEmailRequest
{
    public required string Email { get; set; }
    public required string Name { get; set; }
}
