using System;
using System.Collections.Generic;
using MediatR;
using OrderService.CQRS.DTOs;

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
