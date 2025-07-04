namespace ProductService.Repositories;

using ProductService.Models;

/// <summary>
/// Interface for product repository operations
/// </summary>
public interface IProductRepository
{
    /// <summary>
    /// Gets all products
    /// </summary>
    /// <returns>Collection of all products</returns>
    Task<IEnumerable<Product>> GetAllAsync();

    /// <summary>
    /// Gets a product by its ID
    /// </summary>
    /// <param name="id">The product ID</param>
    /// <returns>The product if found, null otherwise</returns>
    Task<Product?> GetByIdAsync(Guid id);

    /// <summary>
    /// Creates a new product
    /// </summary>
    /// <param name="product">The product to create</param>
    /// <returns>The created product</returns>
    Task<Product> CreateAsync(Product product);

    /// <summary>
    /// Updates an existing product
    /// </summary>
    /// <param name="product">The product with updated values</param>
    /// <returns>The updated product if found, null otherwise</returns>
    Task<Product?> UpdateAsync(Product product);

    /// <summary>
    /// Deletes a product by its ID
    /// </summary>
    /// <param name="id">The product ID</param>
    /// <returns>True if deleted, false if not found</returns>
    Task<bool> DeleteAsync(Guid id);

    /// <summary>
    /// Reserves products for an order
    /// </summary>
    /// <param name="productReservations">List of product IDs and quantities to reserve</param>
    /// <returns>True if all products were reserved, false if any product couldn't be reserved</returns>
    Task<(bool Success, string ErrorMessage)> ReserveProductsAsync(List<(Guid ProductId, int Quantity)> productReservations);

    /// <summary>
    /// Releases previously reserved products
    /// </summary>
    /// <param name="productReservations">List of product IDs and quantities to release</param>
    /// <returns>True if all products were released</returns>
    Task<bool> ReleaseProductsAsync(List<(Guid ProductId, int Quantity)> productReservations);
}
