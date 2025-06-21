using System.Collections.Generic;
using MediatR;
using OrderService.CQRS.DTOs;

namespace OrderService.CQRS.Queries;

/// <summary>
/// Query to get all orders
/// </summary>
public class GetAllOrdersQuery : IRequest<IEnumerable<OrderDto>>
{
    // This query doesn't need any parameters
}