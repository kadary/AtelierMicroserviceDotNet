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

    /// <inheritdoc />
    public Task<(bool Success, string ErrorMessage)> ReserveProductsAsync(List<(Guid ProductId, int Quantity)> productReservations)
    {
        _logger.LogInformation("Attempting to reserve {Count} products", productReservations.Count);

        // Check if all products exist and have enough stock
        foreach (var (productId, quantity) in productReservations)
        {
            if (!_products.TryGetValue(productId, out var product))
            {
                _logger.LogWarning("Product not found for reservation: {ProductId}", productId);
                return Task.FromResult((Success: false, ErrorMessage: $"Product not found: {productId}"));
            }

            if (product.StockQuantity < quantity)
            {
                _logger.LogWarning("Insufficient stock for product {ProductId}. Requested: {Requested}, Available: {Available}", 
                    productId, quantity, product.StockQuantity);
                return Task.FromResult((Success: false, ErrorMessage: $"Insufficient stock for product {product.Name}. Requested: {quantity}, Available: {product.StockQuantity}"));
            }
        }

        // All products exist and have enough stock, so reserve them
        foreach (var (productId, quantity) in productReservations)
        {
            var product = _products[productId];
            product.StockQuantity -= quantity;
            _products[productId] = product;

            _logger.LogInformation("Reserved {Quantity} units of product {ProductId}. New stock: {NewStock}", 
                quantity, productId, product.StockQuantity);
        }

        return Task.FromResult((Success: true, ErrorMessage: string.Empty));
    }

    /// <inheritdoc />
    public Task<bool> ReleaseProductsAsync(List<(Guid ProductId, int Quantity)> productReservations)
    {
        _logger.LogInformation("Releasing {Count} products", productReservations.Count);

        foreach (var (productId, quantity) in productReservations)
        {
            if (_products.TryGetValue(productId, out var product))
            {
                product.StockQuantity += quantity;
                _products[productId] = product;

                _logger.LogInformation("Released {Quantity} units of product {ProductId}. New stock: {NewStock}", 
                    quantity, productId, product.StockQuantity);
            }
            else
            {
                _logger.LogWarning("Product not found for release: {ProductId}", productId);
            }
        }

        return Task.FromResult(true);
    }
}
