using Serilog;
using Serilog.Sinks.Grafana.Loki;
using MassTransit;
using OrderService.Models;
using OrderService.Repositories;
using OrderService.CQRS.Commands;
using OrderService.CQRS.Queries;
using OrderService.CQRS.DTOs;
using OrderService.Sagas;
using OrderService.Sagas.Events;
using Polly;
using Polly.Extensions.Http;
using System.Reflection;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using OpenTelemetry.Logs;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/order-service-.txt", rollingInterval: RollingInterval.Day)
    .WriteTo.GrafanaLoki(
        "http://loki:3100",
        labels: new[] { 
            new LokiLabel { Key = "service", Value = "order-service" },
            new LokiLabel { Key = "environment", Value = builder.Environment.EnvironmentName }
        },
        credentials: null,
        batchPostingLimit: 1000,
        queueLimit: 100000,
        period: TimeSpan.FromSeconds(2),
        textFormatter: new Serilog.Formatting.Json.JsonFormatter()
    )
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "OrderService")
    .CreateLogger();

builder.Host.UseSerilog();

// Create logger for startup configuration
var loggerFactory = LoggerFactory.Create(builder => builder.AddSerilog());
var logger = loggerFactory.CreateLogger<Program>();

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();

// Configure OpenTelemetry
var serviceName = "order-service";
var serviceVersion = "1.0.0";

// Configure OpenTelemetry Resources
var resourceBuilder = ResourceBuilder.CreateDefault()
    .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
    .AddTelemetrySdk()
    .AddEnvironmentVariableDetector();

// Configure OpenTelemetry Tracing
builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            .SetResourceBuilder(resourceBuilder)
            .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
                options.EnrichWithHttpRequest = (activity, httpRequest) =>
                {
                    activity.SetTag("http.request.header.x-request-id", httpRequest.Headers["x-request-id"]);
                };
                options.EnrichWithHttpResponse = (activity, httpResponse) =>
                {
                    activity.SetTag("http.response.header.x-response-id", httpResponse.Headers["x-response-id"]);
                };
            })
            .AddHttpClientInstrumentation(options =>
            {
                options.RecordException = true;
                options.EnrichWithHttpRequestMessage = (activity, request) =>
                {
                    if (request.Headers.Contains("x-request-id"))
                    {
                        activity.SetTag("http.request.header.x-request-id", request.Headers.GetValues("x-request-id").First());
                    }
                };
                options.EnrichWithHttpResponseMessage = (activity, response) =>
                {
                    if (response.Headers.Contains("x-response-id"))
                    {
                        activity.SetTag("http.response.header.x-response-id", response.Headers.GetValues("x-response-id").First());
                    }
                };
            })
            .AddSource("MassTransit") // Add MassTransit as a source
            .AddSource("MediatR") // Add MediatR as a source
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri("http://otel-collector:4317");
            });
    })
    .WithMetrics(metricsProviderBuilder =>
    {
        metricsProviderBuilder
            .SetResourceBuilder(resourceBuilder)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri("http://otel-collector:4317");
            })
            .AddPrometheusExporter();
    });

// Configure JSON serialization options
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { 
        Title = "Order Service API", 
        Version = "v1",
        Description = "A microservice for managing orders and publishing order events"
    });

    // Configure Swagger to handle circular references
    c.CustomSchemaIds(type => type.FullName);
    c.UseAllOfToExtendReferenceSchemas();

    // Add JWT Authentication support to Swagger
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Register repositories
builder.Services.AddSingleton<IOrderRepository, OrderRepository>();

// Register MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

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
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("OrdersReadPolicy", policy =>
        policy.RequireClaim("scope", "orders:read"));
    options.AddPolicy("OrdersWritePolicy", policy =>
        policy.RequireClaim("scope", "orders:write"));
});

    // Configure MassTransit with RabbitMQ and Saga
builder.Services.AddMassTransit(x =>
{
    // Add the OrderSaga state machine
    x.AddSagaStateMachine<OrderSaga, OrderSagaState>()
        .InMemoryRepository();

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

        // Configure the saga endpoint
        cfg.ReceiveEndpoint("order-saga", e =>
        {
            // Configure the state machine saga
            e.ConfigureSaga<OrderSagaState>(context);
            logger.LogInformation("Configured OrderSaga state machine");
        });
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

// Add authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

logger.LogInformation("Starting OrderService");

// Order endpoints
var ordersGroup = app.MapGroup("/api/orders")
    .WithTags("Orders")
    .WithOpenApi()
    .RequireAuthorization(); // Require authorization for all endpoints in this group

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
