using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using OrderService.CQRS.DTOs;
using OrderService.CQRS.Queries;
using OrderService.Repositories;

namespace OrderService.CQRS.Handlers;

/// <summary>
/// Handler for the GetOrderByIdQuery
/// </summary>
public class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, OrderDto?>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<GetOrderByIdQueryHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the GetOrderByIdQueryHandler class
    /// </summary>
    /// <param name="orderRepository">Order repository</param>
    /// <param name="logger">Logger instance</param>
    public GetOrderByIdQueryHandler(
        IOrderRepository orderRepository,
        ILogger<GetOrderByIdQueryHandler> logger)
    {
        _orderRepository = orderRepository;
        _logger = logger;
    }

    /// <summary>
    /// Handles the GetOrderByIdQuery
    /// </summary>
    /// <param name="request">The query to handle</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The requested order, or null if not found</returns>
    public async Task<OrderDto?> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling GetOrderByIdQuery for order ID {OrderId}", request.OrderId);

        var order = await _orderRepository.GetByIdAsync(request.OrderId);

        if (order == null)
        {
            _logger.LogWarning("Order not found: {OrderId}", request.OrderId);
            return null;
        }

        // Map the order entity to the DTO
        var orderDto = new OrderDto
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
        };

        _logger.LogInformation("Order found: {OrderId}", request.OrderId);
        return orderDto;
    }
}