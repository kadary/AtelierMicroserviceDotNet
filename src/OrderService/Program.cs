using Serilog;
using MassTransit;
using OrderService.Models;
using OrderService.Messages;
using OrderService.Repositories;
using Polly;
using Polly.Extensions.Http;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/order-service-.txt", rollingInterval: RollingInterval.Day)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { 
        Title = "Order Service API", 
        Version = "v1",
        Description = "A microservice for managing orders and publishing order events"
    });
});

// Register repositories
builder.Services.AddSingleton<IOrderRepository, OrderRepository>();

// Configure MassTransit with RabbitMQ
builder.Services.AddMassTransit(x =>
{
    // Set up endpoint name formatter
    x.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter(prefix: "order", includeNamespace: false));

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

        // Configure retry policy
        cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));

        cfg.ConfigureEndpoints(context);
    });
});

// Configure resilient HTTP client with Polly
builder.Services.AddHttpClient("ProductService", client =>
{
    client.BaseAddress = new Uri("http://product-service:5001");
})
.AddPolicyHandler(GetRetryPolicy());

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

app.UseSerilogRequestLogging();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Starting OrderService");

// Order endpoints
var ordersGroup = app.MapGroup("/api/orders")
    .WithTags("Orders")
    .WithOpenApi();

// Get all orders
ordersGroup.MapGet("/", async (IOrderRepository repository) =>
{
    logger.LogInformation("Request received: Get all orders");
    var orders = await repository.GetAllAsync();
    return Results.Ok(orders);
})
.WithName("GetAllOrders")
.WithDescription("Gets all orders")
.Produces<IEnumerable<Order>>(StatusCodes.Status200OK);

// Get order by ID
ordersGroup.MapGet("/{id}", async (Guid id, IOrderRepository repository) =>
{
    logger.LogInformation("Request received: Get order by ID {Id}", id);
    var order = await repository.GetByIdAsync(id);

    if (order == null)
    {
        logger.LogWarning("Order not found: {Id}", id);
        return Results.NotFound();
    }

    return Results.Ok(order);
})
.WithName("GetOrderById")
.WithDescription("Gets an order by its unique identifier")
.Produces<Order>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);

// Create order
ordersGroup.MapPost("/", async (Order order, IOrderRepository repository, IPublishEndpoint publishEndpoint) =>
{
    logger.LogInformation("Request received: Create order for customer {CustomerName}", order.CustomerName);

    try
    {
        // Save the order
        var createdOrder = await repository.CreateAsync(order);

        // Create and publish the OrderCreated event
        var orderCreatedEvent = new OrderCreated
        {
            OrderId = createdOrder.Id,
            CustomerName = createdOrder.CustomerName,
            CustomerEmail = createdOrder.CustomerEmail,
            TotalAmount = createdOrder.TotalAmount,
            ItemCount = createdOrder.Items.Count,
            CreatedAt = createdOrder.CreatedAt
        };

        logger.LogInformation("Publishing OrderCreated event for order {OrderId}", createdOrder.Id);
        await publishEndpoint.Publish(orderCreatedEvent);

        logger.LogInformation("Order created and event published: {Id}", createdOrder.Id);
        return Results.Created($"/api/orders/{createdOrder.Id}", createdOrder);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error creating order for customer {CustomerName}", order.CustomerName);
        return Results.Problem("An error occurred while creating the order.");
    }
})
.WithName("CreateOrder")
.WithDescription("Creates a new order and publishes an OrderCreated event")
.Produces<Order>(StatusCodes.Status201Created)
.Produces(StatusCodes.Status500InternalServerError);

// Update order status
ordersGroup.MapPut("/{id}/status", async (Guid id, OrderStatus status, IOrderRepository repository) =>
{
    logger.LogInformation("Request received: Update order status {Id} to {Status}", id, status);

    var updatedOrder = await repository.UpdateStatusAsync(id, status);

    if (updatedOrder == null)
    {
        logger.LogWarning("Order not found for status update: {Id}", id);
        return Results.NotFound();
    }

    logger.LogInformation("Order status updated: {Id} to {Status}", id, status);
    return Results.Ok(updatedOrder);
})
.WithName("UpdateOrderStatus")
.WithDescription("Updates the status of an existing order")
.Produces<Order>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);

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

// Define the retry policy for HTTP requests
static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
}
