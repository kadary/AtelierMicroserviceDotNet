using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using OrderService.CQRS.Commands;
using OrderService.Repositories;

namespace OrderService.CQRS.Handlers;

/// <summary>
/// Handler for the UpdateOrderStatusCommand
/// </summary>
public class UpdateOrderStatusCommandHandler : IRequestHandler<UpdateOrderStatusCommand, bool>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<UpdateOrderStatusCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the UpdateOrderStatusCommandHandler class
    /// </summary>
    /// <param name="orderRepository">Order repository</param>
    /// <param name="logger">Logger instance</param>
    public UpdateOrderStatusCommandHandler(
        IOrderRepository orderRepository,
        ILogger<UpdateOrderStatusCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _logger = logger;
    }

    /// <summary>
    /// Handles the UpdateOrderStatusCommand
    /// </summary>
    /// <param name="request">The command to handle</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the order status was updated, false otherwise</returns>
    public async Task<bool> Handle(UpdateOrderStatusCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling UpdateOrderStatusCommand for order ID {OrderId} to status {Status}", 
            request.OrderId, request.Status);

        var updatedOrder = await _orderRepository.UpdateStatusAsync(request.OrderId, request.Status);

        if (updatedOrder == null)
        {
            _logger.LogWarning("Order not found for status update: {OrderId}", request.OrderId);
            return false;
        }

        _logger.LogInformation("Order status updated: {OrderId} to {Status}", request.OrderId, request.Status);
        return true;
    }
}