using System;
using MediatR;
using OrderService.CQRS.DTOs;

namespace OrderService.CQRS.Queries;

/// <summary>
/// Query to get an order by its ID
/// </summary>
public class GetOrderByIdQuery : IRequest<OrderDto?>
{
    /// <summary>
    /// The ID of the order to retrieve
    /// </summary>
    public Guid OrderId { get; set; }

    /// <summary>
    /// Initializes a new instance of the GetOrderByIdQuery class
    /// </summary>
    /// <param name="orderId">The ID of the order to retrieve</param>
    public GetOrderByIdQuery(Guid orderId)
    {
        OrderId = orderId;
    }
}