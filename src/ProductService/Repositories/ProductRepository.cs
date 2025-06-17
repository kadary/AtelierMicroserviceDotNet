namespace ProductService.Repositories;

using System.Collections.Concurrent;
using ProductService.Models;

/// <summary>
/// In-memory implementation of the product repository
/// </summary>
public class ProductRepository : IProductRepository
{
    private readonly ConcurrentDictionary<Guid, Product> _products = new();
    private readonly ILogger<ProductRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the ProductRepository class
    /// </summary>
    /// <param name="logger">Logger instance</param>
    public ProductRepository(ILogger<ProductRepository> logger)
    {
        _logger = logger;
        // Add some sample products
        var sampleProducts = new List<Product>
        {
            new Product { Name = "Laptop", Description = "High-performance laptop", Price = 1299.99m, StockQuantity = 10 },
            new Product { Name = "Smartphone", Description = "Latest smartphone model", Price = 899.99m, StockQuantity = 15 },
            new Product { Name = "Headphones", Description = "Noise-cancelling headphones", Price = 199.99m, StockQuantity = 20 }
        };

        foreach (var product in sampleProducts)
        {
            _products.TryAdd(product.Id, product);
        }
        
        _logger.LogInformation("ProductRepository initialized with {Count} sample products", _products.Count);
    }

    /// <inheritdoc />
    public Task<IEnumerable<Product>> GetAllAsync()
    {
        _logger.LogInformation("Getting all products. Count: {Count}", _products.Count);
        return Task.FromResult<IEnumerable<Product>>(_products.Values.ToList());
    }

    /// <inheritdoc />
    public Task<Product?> GetByIdAsync(Guid id)
    {
        _logger.LogInformation("Getting product by ID: {Id}", id);
        _products.TryGetValue(id, out var product);
        return Task.FromResult(product);
    }

    /// <inheritdoc />
    public Task<Product> CreateAsync(Product product)
    {
        _logger.LogInformation("Creating new product: {Name}", product.Name);
        _products.TryAdd(product.Id, product);
        return Task.FromResult(product);
    }

    /// <inheritdoc />
    public Task<Product?> UpdateAsync(Product product)
    {
        _logger.LogInformation("Updating product: {Id}", product.Id);
        if (_products.TryGetValue(product.Id, out _))
        {
            _products[product.Id] = product;
            return Task.FromResult<Product?>(product);
        }
        
        _logger.LogWarning("Product not found for update: {Id}", product.Id);
        return Task.FromResult<Product?>(null);
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(Guid id)
    {
        _logger.LogInformation("Deleting product: {Id}", id);
        var result = _products.TryRemove(id, out _);
        if (!result)
        {
            _logger.LogWarning("Product not found for deletion: {Id}", id);
        }
        return Task.FromResult(result);
    }
}