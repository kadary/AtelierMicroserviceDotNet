using Serilog;
using Serilog.Sinks.OpenTelemetry;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Test;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using OpenTelemetry.Logs;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/identity-server-.txt", rollingInterval: RollingInterval.Day)
    .WriteTo.OpenTelemetry(options =>
    {
        options.Endpoint = "http://otel-collector:4317";
        options.Protocol = OtlpProtocol.Grpc;
        options.ResourceAttributes = new Dictionary<string, object>
        {
            ["service.name"] = "identity-server",
            ["service.environment"] = builder.Environment.EnvironmentName
        };
    })
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "IdentityServer")
    .CreateLogger();

builder.Host.UseSerilog();

// Configure OpenTelemetry
var serviceName = "identity-server";
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
            })
            .AddHttpClientInstrumentation(options =>
            {
                options.RecordException = true;
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

// Add IdentityServer
builder.Services.AddIdentityServer(options =>
{
    options.IssuerUri = "http://identity-server:5004"; // Set explicit issuer URI
})
    .AddInMemoryClients(Config.Clients)
    .AddInMemoryApiScopes(Config.ApiScopes)
    .AddInMemoryApiResources(Config.ApiResources)
    .AddInMemoryIdentityResources(Config.IdentityResources)
    .AddTestUsers(Config.Users)
    .AddDeveloperSigningCredential(); // Not recommended for production

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
app.UseSerilogRequestLogging();

app.UseCors("CorsPolicy");

// Health check endpoint
app.MapGet("/health", () =>
{
    Log.Information("Health check requested");
    return Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow });
})
.WithName("HealthCheck")
.WithDescription("Returns the health status of the Identity Server");

app.UseIdentityServer();

app.Run();

// Configuration classes
public static class Config
{
    public static IEnumerable<Client> Clients =>
        new List<Client>
        {
            new Client
            {
                ClientId = "api-gateway",
                ClientName = "API Gateway Client",
                AllowedGrantTypes = GrantTypes.ClientCredentials,
                ClientSecrets = { new Secret("secret".Sha256()) },
                AllowedScopes = { "orders:read", "orders:write", "products:read", "products:write" }
            }
        };

    public static IEnumerable<ApiScope> ApiScopes =>
        new List<ApiScope>
        {
            new ApiScope("orders:read", "Read access to orders API"),
            new ApiScope("orders:write", "Write access to orders API"),
            new ApiScope("products:read", "Read access to products API"),
            new ApiScope("products:write", "Write access to products API")
        };

    public static IEnumerable<ApiResource> ApiResources =>
        new List<ApiResource>
        {
            new ApiResource("orders", "Orders API")
            {
                Scopes = { "orders:read", "orders:write" }
            },
            new ApiResource("products", "Products API")
            {
                Scopes = { "products:read", "products:write" }
            }
        };

    public static IEnumerable<IdentityResource> IdentityResources =>
        new List<IdentityResource>
        {
            new IdentityResources.OpenId(),
            new IdentityResources.Profile()
        };

    public static List<TestUser> Users =>
        new List<TestUser>
        {
            new TestUser
            {
                SubjectId = "1",
                Username = "alice",
                Password = "alice"
            },
            new TestUser
            {
                SubjectId = "2",
                Username = "bob",
                Password = "bob"
            }
        };
}
