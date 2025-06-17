using System;
using System.Collections.Generic;
using OrderService.CQRS.Commands;

namespace OrderService.Sagas.Events;

/// <summary>
/// Event published when a new order is created
/// </summary>
public class OrderCreatedEvent
{
    /// <summary>
    /// Unique identifier of the order
    /// </summary>
    public Guid OrderId { get; set; }
    
    /// <summary>
    /// Customer identifier
    /// </summary>
    public Guid CustomerId { get; set; }
    
    /// <summary>
    /// Name of the customer who placed the order
    /// </summary>
    public required string CustomerName { get; set; }
    
    /// <summary>
    /// Email of the customer for notifications
    /// </summary>
    public required string CustomerEmail { get; set; }
    
    /// <summary>
    /// List of items in the order
    /// </summary>
    public required List<OrderItemDto> Items { get; set; } = new();
    
    /// <summary>
    /// Total amount of the order
    /// </summary>
    public decimal TotalAmount { get; set; }
    
    /// <summary>
    /// Date and time when the order was created
    /// </summary>
    public DateTime CreatedAt { get; set; }
}