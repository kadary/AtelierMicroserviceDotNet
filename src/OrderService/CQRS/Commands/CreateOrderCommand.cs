using System;
using System.Collections.Generic;
using MediatR;

namespace OrderService.CQRS.Commands;

/// <summary>
/// Command to create a new order
/// </summary>
public class CreateOrderCommand : IRequest<Guid>
{
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
    /// List of items in the order
    /// </summary>
    public required List<OrderItemDto> Items { get; set; } = new();
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
    public required string ProductName { get; set; }

    /// <summary>
    /// Price per unit
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Quantity ordered
    /// </summary>
    public int Quantity { get; set; }
}
