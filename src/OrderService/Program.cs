using Serilog;
using MassTransit;
using OrderService.Models;
using OrderService.Messages;
using OrderService.Repositories;
using OrderService.CQRS.Commands;
using OrderService.CQRS.Queries;
using OrderService.CQRS.DTOs;
using Polly;
using Polly.Extensions.Http;
using System.Text.Json;
using System.Reflection;

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

// Register MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

// Configure MassTransit with RabbitMQ - Simplified configuration
builder.Services.AddMassTransit(x =>
{
    // Configure RabbitMQ as the message broker
    x.UsingRabbitMq((context, cfg) =>
    {
        // Configure RabbitMQ connection
        cfg.Host("rabbitmq", "/", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });

        // Configure retry policy
        cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
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
ordersGroup.MapGet("/", async (IMediator mediator, ILogger<Program> logger) =>
{
    logger.LogInformation("Request received: Get all orders");
    var orders = await mediator.Send(new GetAllOrdersQuery());
    return Results.Ok(orders);
})
.WithName("GetAllOrders")
.WithDescription("Gets all orders")
.Produces<IEnumerable<OrderDto>>(StatusCodes.Status200OK);

// Get order by ID
ordersGroup.MapGet("/{id}", async (Guid id, IMediator mediator, ILogger<Program> logger) =>
{
    logger.LogInformation("Request received: Get order by ID {Id}", id);
    var order = await mediator.Send(new GetOrderByIdQuery(id));

    if (order == null)
    {
        logger.LogWarning("Order not found: {Id}", id);
        return Results.NotFound();
    }

    return Results.Ok(order);
})
.WithName("GetOrderById")
.WithDescription("Gets an order by its unique identifier")
.Produces<OrderDto>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);

// Create order
ordersGroup.MapPost("/", async (CreateOrderCommand command, IMediator mediator, ILogger<Program> logger) =>
{
    logger.LogInformation("Request received: Create order for customer {CustomerName}", command.CustomerName);

    try
    {
        // Send the command to create the order
        var orderId = await mediator.Send(command);
        logger.LogInformation("Order created with ID: {OrderId}", orderId);

        // Get the created order
        var createdOrder = await mediator.Send(new GetOrderByIdQuery(orderId));

        return Results.Created($"/api/orders/{orderId}", createdOrder);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error creating order for customer {CustomerName}", command.CustomerName);
        return Results.Problem("An error occurred while creating the order.");
    }
})
.WithName("CreateOrder")
.WithDescription("Creates a new order using CQRS pattern")
.Produces<OrderDto>(StatusCodes.Status201Created)
.Produces(StatusCodes.Status500InternalServerError);

// Update order status
ordersGroup.MapPut("/{id}/status", async (Guid id, OrderStatus status, IMediator mediator, ILogger<Program> logger) =>
{
    logger.LogInformation("Request received: Update order status {Id} to {Status}", id, status);

    var command = new UpdateOrderStatusCommand(id, status);
    var result = await mediator.Send(command);

    if (!result)
    {
        logger.LogWarning("Order not found for status update: {Id}", id);
        return Results.NotFound();
    }

    // Get the updated order
    var updatedOrder = await mediator.Send(new GetOrderByIdQuery(id));

    logger.LogInformation("Order status updated: {Id} to {Status}", id, status);
    return Results.Ok(updatedOrder);
})
.WithName("UpdateOrderStatus")
.WithDescription("Updates the status of an existing order")
.Produces<OrderDto>(StatusCodes.Status200OK)
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
