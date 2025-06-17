using System;
using System.Collections.Generic;
using OrderService.Models;

namespace OrderService.CQRS.DTOs;

/// <summary>
/// Data transfer object for an order
/// </summary>
public class OrderDto
{
    /// <summary>
    /// Unique identifier for the order
    /// </summary>
    public Guid Id { get; set; }
    
    /// <summary>
    /// Customer identifier
    /// </summary>
    public Guid CustomerId { get; set; }
    
    /// <summary>
    /// Customer name
    /// </summary>
    public string CustomerName { get; set; } = string.Empty;
    
    /// <summary>
    /// Customer email for notifications
    /// </summary>
    public string CustomerEmail { get; set; } = string.Empty;
    
    /// <summary>
    /// List of items in the order
    /// </summary>
    public List<OrderItemDto> Items { get; set; } = new();
    
    /// <summary>
    /// Total amount of the order
    /// </summary>
    public decimal TotalAmount { get; set; }
    
    /// <summary>
    /// Date and time when the order was created
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// Current status of the order
    /// </summary>
    public OrderStatus Status { get; set; }
}

/// <summary>
/// Data transfer object for an order item
/// </summary>
public class OrderItemDto
{
    /// <summary>
    /// Product identifier
    /// </summary>
    public Guid ProductId { get; set; }
    
    /// <summary>
    /// Product name
    /// </summary>
    public string ProductName { get; set; } = string.Empty;
    
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