using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using OrderService.CQRS.DTOs;
using OrderService.CQRS.Queries;
using OrderService.Repositories;

namespace OrderService.CQRS.Handlers;

/// <summary>
/// Handler for the GetAllOrdersQuery
/// </summary>
public class GetAllOrdersQueryHandler : IRequestHandler<GetAllOrdersQuery, IEnumerable<OrderDto>>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<GetAllOrdersQueryHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the GetAllOrdersQueryHandler class
    /// </summary>
    /// <param name="orderRepository">Order repository</param>
    /// <param name="logger">Logger instance</param>
    public GetAllOrdersQueryHandler(
        IOrderRepository orderRepository,
        ILogger<GetAllOrdersQueryHandler> logger)
    {
        _orderRepository = orderRepository;
        _logger = logger;
    }

    /// <summary>
    /// Handles the GetAllOrdersQuery
    /// </summary>
    /// <param name="request">The query to handle</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>All orders</returns>
    public async Task<IEnumerable<OrderDto>> Handle(GetAllOrdersQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling GetAllOrdersQuery");

        var orders = await _orderRepository.GetAllAsync();

        // Map the order entities to DTOs
        var orderDtos = orders.Select(order => new OrderDto
        {
            Id = order.Id,
            CustomerId = order.CustomerId,
            CustomerName = order.CustomerName,
            CustomerEmail = order.CustomerEmail,
            TotalAmount = order.TotalAmount,
            CreatedAt = order.CreatedAt,
            Status = order.Status,
            Items = order.Items.Select(item => new OrderItemDto
            {
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                UnitPrice = item.UnitPrice,
                Quantity = item.Quantity
            }).ToList()
        }).ToList();

        _logger.LogInformation("Retrieved {Count} orders", orderDtos.Count);
        return orderDtos;
    }
}