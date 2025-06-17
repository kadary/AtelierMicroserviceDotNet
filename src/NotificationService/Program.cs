using Serilog;
using MassTransit;
using NotificationService.Consumers;
using NotificationService.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/notification-service-.txt", rollingInterval: RollingInterval.Day)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();

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

// Configure MassTransit with RabbitMQ
builder.Services.AddMassTransit(x =>
{
    // Add consumer
    x.AddConsumer<OrderCreatedConsumer>();

    // Set up endpoint name formatter
    x.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter(prefix: "notification", includeNamespace: false));

    // Configure JSON serialization to handle different namespaces
    x.ConfigureJsonSerializerOptions(options => 
    {
        options.PropertyNameCaseInsensitive = true;
        return options;
    });

    x.UsingRabbitMq((context, cfg) =>
    {
        // Configure RabbitMQ connection
        cfg.Host("rabbitmq", "/", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });

        // Configure JSON serializer
        cfg.ConfigureJsonSerializerOptions(options =>
        {
            options.PropertyNameCaseInsensitive = true;
            options.WriteIndented = true;
            return options;
        });

        // Configure consumer endpoint
        cfg.ReceiveEndpoint("order-created", e =>
        {
            // Configure consumer
            e.ConfigureConsumer<OrderCreatedConsumer>(context);

            // Configure retry policy
            e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));

            // Set up message binding
            e.ConfigureConsumeTopology = false;

            // Bind to the exchange created by MassTransit for the OrderCreated message
            e.Bind("OrderService.Messages:OrderCreated");
        });
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

app.UseSerilogRequestLogging();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Starting NotificationService");

// Notification endpoints
var notificationsGroup = app.MapGroup("/api/notifications")
    .WithTags("Notifications")
    .WithOpenApi();

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
