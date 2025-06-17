namespace OrderService.Models;

/// <summary>
/// Represents an order in the system
/// </summary>
public class Order
{
    /// <summary>
    /// Unique identifier for the order
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Customer identifier
    /// </summary>
    public Guid CustomerId { get; set; }
    
    /// <summary>
    /// Customer name
    /// </summary>
    public required string CustomerName { get; set; }
    
    /// <summary>
    /// Customer email for notifications
    /// </summary>
    public required string CustomerEmail { get; set; }
    
    /// <summary>
    /// List of products in the order
    /// </summary>
    public required List<OrderItem> Items { get; set; } = new();
    
    /// <summary>
    /// Total amount of the order
    /// </summary>
    public decimal TotalAmount { get; set; }
    
    /// <summary>
    /// Date and time when the order was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Current status of the order
    /// </summary>
    public OrderStatus Status { get; set; } = OrderStatus.Created;
}

/// <summary>
/// Represents an item in an order
/// </summary>
public class OrderItem
{
    /// <summary>
    /// Product identifier
    /// </summary>
    public Guid ProductId { get; set; }
    
    /// <summary>
    /// Product name
    /// </summary>
    public required string ProductName { get; set; }
    
    /// <summary>
    /// Price per unit
    /// </summary>
    public decimal UnitPrice { get; set; }
    
    /// <summary>
    /// Quantity ordered
    /// </summary>
    public int Quantity { get; set; }
    
    /// <summary>
    /// Total price for this item (UnitPrice * Quantity)
    /// </summary>
    public decimal TotalPrice => UnitPrice * Quantity;
}

/// <summary>
/// Represents the status of an order
/// </summary>
public enum OrderStatus
{
    /// <summary>
    /// Order has been created
    /// </summary>
    Created,
    
    /// <summary>
    /// Order has been processed
    /// </summary>
    Processed,
    
    /// <summary>
    /// Order has been shipped
    /// </summary>
    Shipped,
    
    /// <summary>
    /// Order has been delivered
    /// </summary>
    Delivered,
    
    /// <summary>
    /// Order has been cancelled
    /// </summary>
    Cancelled
}