using Serilog;
using Serilog.Sinks.OpenTelemetry;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
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
    .WriteTo.File("logs/api-gateway-.txt", rollingInterval: RollingInterval.Day)
    .WriteTo.OpenTelemetry(options =>
    {
        options.Endpoint = "http://otel-collector:4317";
        options.Protocol = OtlpProtocol.Grpc;
        options.ResourceAttributes = new Dictionary<string, object>
        {
            ["service.name"] = "api-gateway",
            ["service.environment"] = builder.Environment.EnvironmentName
        };
    })
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "ApiGateway")
    .CreateLogger();

builder.Host.UseSerilog();

// Configure OpenTelemetry
var serviceName = "api-gateway";
var serviceVersion = "1.0.0";

// Configure OpenTelemetry Resources
var resourceBuilder = ResourceBuilder.CreateDefault()
    .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
    .AddTelemetrySdk()
    .AddEnvironmentVariableDetector();

// Configure OpenTelemetry Tracing and Metrics
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
            });
    });

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
    options.AddPolicy("ProductsReadPolicy", policy =>
        policy.RequireClaim("scope", "products:read"));
    options.AddPolicy("ProductsWritePolicy", policy =>
        policy.RequireClaim("scope", "products:write"));
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

// Add authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

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
