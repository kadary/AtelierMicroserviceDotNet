using Serilog;
using ProductService.Models;
using ProductService.Repositories;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/product-service-.txt", rollingInterval: RollingInterval.Day)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { 
        Title = "Product Service API", 
        Version = "v1",
        Description = "A microservice for managing products"
    });
});

// Register repositories
builder.Services.AddSingleton<IProductRepository, ProductRepository>();

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
    options.AddPolicy("ProductsReadPolicy", policy =>
        policy.RequireClaim("scope", "products:read"));
    options.AddPolicy("ProductsWritePolicy", policy =>
        policy.RequireClaim("scope", "products:write"));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

app.UseSerilogRequestLogging();

// Add authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Starting ProductService");

// Product endpoints
var productsGroup = app.MapGroup("/api/products")
    .WithTags("Products")
    .WithOpenApi()
    .RequireAuthorization(); // Require authorization for all endpoints in this group

// Get all products
productsGroup.MapGet("/", async (IProductRepository repository) =>
{
    logger.LogInformation("Request received: Get all products");
    var products = await repository.GetAllAsync();
    logger.LogInformation("Returning {Count} products", products.Count());
    return Results.Ok(products);
})
.WithName("GetAllProducts")
.WithDescription("Gets all available products")
.Produces<IEnumerable<Product>>(StatusCodes.Status200OK);

// Get product by ID
productsGroup.MapGet("/{id}", async (Guid id, IProductRepository repository) =>
{
    logger.LogInformation("Request received: Get product by ID {Id}", id);
    var product = await repository.GetByIdAsync(id);

    if (product == null)
    {
        logger.LogWarning("Product not found: {Id}", id);
        return Results.NotFound();
    }

    return Results.Ok(product);
})
.WithName("GetProductById")
.WithDescription("Gets a product by its unique identifier")
.Produces<Product>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);

// Create product
productsGroup.MapPost("/", async (Product product, IProductRepository repository) =>
{
    logger.LogInformation("Request received: Create product {ProductName} with price {Price} and stock {Stock}", 
        product.Name, product.Price, product.StockQuantity);
    var createdProduct = await repository.CreateAsync(product);

    logger.LogInformation("Product created: {Id}, Name: {Name}, Price: {Price}, Stock: {Stock}", 
        createdProduct.Id, createdProduct.Name, createdProduct.Price, createdProduct.StockQuantity);
    return Results.Created($"/api/products/{createdProduct.Id}", createdProduct);
})
.WithName("CreateProduct")
.WithDescription("Creates a new product")
.Produces<Product>(StatusCodes.Status201Created);

// Update product
productsGroup.MapPut("/{id}", async (Guid id, Product product, IProductRepository repository) =>
{
    logger.LogInformation("Request received: Update product {Id}", id);

    // Ensure the ID in the URL matches the product
    product.Id = id;

    var updatedProduct = await repository.UpdateAsync(product);

    if (updatedProduct == null)
    {
        logger.LogWarning("Product not found for update: {Id}", id);
        return Results.NotFound();
    }

    logger.LogInformation("Product updated: {Id}", id);
    return Results.Ok(updatedProduct);
})
.WithName("UpdateProduct")
.WithDescription("Updates an existing product")
.Produces<Product>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);

// Delete product
productsGroup.MapDelete("/{id}", async (Guid id, IProductRepository repository) =>
{
    logger.LogInformation("Request received: Delete product {Id}", id);
    var deleted = await repository.DeleteAsync(id);

    if (!deleted)
    {
        logger.LogWarning("Product not found for deletion: {Id}", id);
        return Results.NotFound();
    }

    logger.LogInformation("Product deleted: {Id}", id);
    return Results.NoContent();
})
.WithName("DeleteProduct")
.WithDescription("Deletes a product")
.Produces(StatusCodes.Status204NoContent)
.Produces(StatusCodes.Status404NotFound);

// Reserve products endpoint
productsGroup.MapPost("/reserve", async (ReserveProductsRequest request, IProductRepository repository, ILogger<Program> logger) =>
{
    logger.LogInformation("Request received: Reserve products for order {OrderId}", request.OrderId);

    try
    {
        // Convert the request to the format expected by the repository
        var productReservations = request.Items.Select(item => (item.ProductId, item.Quantity)).ToList();

        // Attempt to reserve the products
        var (success, errorMessage) = await repository.ReserveProductsAsync(productReservations);

        if (success)
        {
            logger.LogInformation("Products reserved successfully for order {OrderId}", request.OrderId);
            return Results.Ok(new { Success = true });
        }
        else
        {
            logger.LogWarning("Failed to reserve products for order {OrderId}: {ErrorMessage}", request.OrderId, errorMessage);
            return Results.BadRequest(new { Success = false, ErrorMessage = errorMessage });
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error reserving products for order {OrderId}", request.OrderId);
        return Results.Problem("An error occurred while reserving products.");
    }
})
.WithName("ReserveProducts")
.WithDescription("Reserves products for an order")
.Produces<object>(StatusCodes.Status200OK)
.Produces<object>(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status500InternalServerError);

// Release products endpoint
productsGroup.MapPost("/release", async (ReleaseProductsRequest request, IProductRepository repository, ILogger<Program> logger) =>
{
    logger.LogInformation("Request received: Release products for order {OrderId}", request.OrderId);

    try
    {
        // Convert the request to the format expected by the repository
        var productReservations = request.Items.Select(item => (item.ProductId, item.Quantity)).ToList();

        // Release the products
        var success = await repository.ReleaseProductsAsync(productReservations);

        if (success)
        {
            logger.LogInformation("Products released successfully for order {OrderId}", request.OrderId);
            return Results.Ok(new { Success = true });
        }
        else
        {
            logger.LogWarning("Failed to release products for order {OrderId}", request.OrderId);
            return Results.BadRequest(new { Success = false });
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error releasing products for order {OrderId}", request.OrderId);
        return Results.Problem("An error occurred while releasing products.");
    }
})
.WithName("ReleaseProducts")
.WithDescription("Releases previously reserved products")
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

// Define request models
public class ReserveProductsRequest
{
    public Guid OrderId { get; set; }
    public List<ProductReservationItem> Items { get; set; } = new();
}

public class ReleaseProductsRequest
{
    public Guid OrderId { get; set; }
    public List<ProductReservationItem> Items { get; set; } = new();
}

public class ProductReservationItem
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
}
