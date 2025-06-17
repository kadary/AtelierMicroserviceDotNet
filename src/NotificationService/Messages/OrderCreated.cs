namespace OrderService.Messages;

/// <summary>
/// Event message received when a new order is created
/// </summary>
public class OrderCreated
{
    /// <summary>
    /// Unique identifier of the order
    /// </summary>
    public Guid OrderId { get; set; }

    /// <summary>
    /// Name of the customer who placed the order
    /// </summary>
    public required string CustomerName { get; set; }

    /// <summary>
    /// Email of the customer for notifications
    /// </summary>
    public required string CustomerEmail { get; set; }

    /// <summary>
    /// Total amount of the order
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Number of items in the order
    /// </summary>
    public int ItemCount { get; set; }

    /// <summary>
    /// Date and time when the order was created
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
