namespace OrderService.Repositories;

using System.Collections.Concurrent;
using OrderService.Models;

/// <summary>
/// In-memory implementation of the order repository
/// </summary>
public class OrderRepository : IOrderRepository
{
    private readonly ConcurrentDictionary<Guid, Order> _orders = new();
    private readonly ILogger<OrderRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the OrderRepository class
    /// </summary>
    /// <param name="logger">Logger instance</param>
    public OrderRepository(ILogger<OrderRepository> logger)
    {
        _logger = logger;
        _logger.LogInformation("OrderRepository initialized");
    }

    /// <inheritdoc />
    public Task<IEnumerable<Order>> GetAllAsync()
    {
        _logger.LogInformation("Getting all orders. Count: {Count}", _orders.Count);
        return Task.FromResult<IEnumerable<Order>>(_orders.Values.ToList());
    }

    /// <inheritdoc />
    public Task<Order?> GetByIdAsync(Guid id)
    {
        _logger.LogInformation("Getting order by ID: {Id}", id);
        _orders.TryGetValue(id, out var order);
        return Task.FromResult(order);
    }

    /// <inheritdoc />
    public Task<Order> CreateAsync(Order order)
    {
        _logger.LogInformation("Creating new order for customer: {CustomerName}", order.CustomerName);
        
        // Calculate total amount based on items
        order.TotalAmount = order.Items.Sum(item => item.TotalPrice);
        
        _orders.TryAdd(order.Id, order);
        _logger.LogInformation("Order created with ID: {Id}, Total Amount: {TotalAmount}", order.Id, order.TotalAmount);
        
        return Task.FromResult(order);
    }

    /// <inheritdoc />
    public Task<Order?> UpdateAsync(Order order)
    {
        _logger.LogInformation("Updating order: {Id}", order.Id);
        
        if (_orders.TryGetValue(order.Id, out _))
        {
            // Recalculate total amount
            order.TotalAmount = order.Items.Sum(item => item.TotalPrice);
            
            _orders[order.Id] = order;
            _logger.LogInformation("Order updated: {Id}, New Total Amount: {TotalAmount}", order.Id, order.TotalAmount);
            return Task.FromResult<Order?>(order);
        }
        
        _logger.LogWarning("Order not found for update: {Id}", order.Id);
        return Task.FromResult<Order?>(null);
    }

    /// <inheritdoc />
    public Task<Order?> UpdateStatusAsync(Guid id, OrderStatus status)
    {
        _logger.LogInformation("Updating order status: {Id} to {Status}", id, status);
        
        if (_orders.TryGetValue(id, out var order))
        {
            order.Status = status;
            _logger.LogInformation("Order status updated: {Id} to {Status}", id, status);
            return Task.FromResult<Order?>(order);
        }
        
        _logger.LogWarning("Order not found for status update: {Id}", id);
        return Task.FromResult<Order?>(null);
    }
}