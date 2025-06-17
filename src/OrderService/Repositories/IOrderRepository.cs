namespace OrderService.Repositories;

using OrderService.Models;

/// <summary>
/// Interface for order repository operations
/// </summary>
public interface IOrderRepository
{
    /// <summary>
    /// Gets all orders
    /// </summary>
    /// <returns>Collection of all orders</returns>
    Task<IEnumerable<Order>> GetAllAsync();
    
    /// <summary>
    /// Gets an order by its ID
    /// </summary>
    /// <param name="id">The order ID</param>
    /// <returns>The order if found, null otherwise</returns>
    Task<Order?> GetByIdAsync(Guid id);
    
    /// <summary>
    /// Creates a new order
    /// </summary>
    /// <param name="order">The order to create</param>
    /// <returns>The created order</returns>
    Task<Order> CreateAsync(Order order);
    
    /// <summary>
    /// Updates an existing order
    /// </summary>
    /// <param name="order">The order with updated values</param>
    /// <returns>The updated order if found, null otherwise</returns>
    Task<Order?> UpdateAsync(Order order);
    
    /// <summary>
    /// Updates the status of an order
    /// </summary>
    /// <param name="id">The order ID</param>
    /// <param name="status">The new status</param>
    /// <returns>The updated order if found, null otherwise</returns>
    Task<Order?> UpdateStatusAsync(Guid id, OrderStatus status);
}