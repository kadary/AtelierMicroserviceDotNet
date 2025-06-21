using System;
using MediatR;
using OrderService.Models;

namespace OrderService.CQRS.Commands;

/// <summary>
/// Command to update the status of an order
/// </summary>
public class UpdateOrderStatusCommand : IRequest<bool>
{
    /// <summary>
    /// The ID of the order to update
    /// </summary>
    public Guid OrderId { get; set; }
    
    /// <summary>
    /// The new status for the order
    /// </summary>
    public OrderStatus Status { get; set; }
    
    /// <summary>
    /// Initializes a new instance of the UpdateOrderStatusCommand class
    /// </summary>
    /// <param name="orderId">The ID of the order to update</param>
    /// <param name="status">The new status for the order</param>
    public UpdateOrderStatusCommand(Guid orderId, OrderStatus status)
    {
        OrderId = orderId;
        Status = status;
    }
}