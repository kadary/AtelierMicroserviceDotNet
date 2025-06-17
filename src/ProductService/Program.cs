using Serilog;
using ProductService.Models;
using ProductService.Repositories;
using System.Text.Json;

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

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

app.UseSerilogRequestLogging();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Starting ProductService");

// Product endpoints
var productsGroup = app.MapGroup("/api/products")
    .WithTags("Products")
    .WithOpenApi();

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
