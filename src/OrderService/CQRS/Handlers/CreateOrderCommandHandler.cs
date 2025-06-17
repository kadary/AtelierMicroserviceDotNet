using System;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using OrderService.CQRS.Commands;
using OrderService.Models;
using OrderService.Repositories;
using OrderService.Sagas.Events;
using OrderService.Messages;

namespace OrderService.CQRS.Handlers;

/// <summary>
/// Handler for the CreateOrderCommand
/// </summary>
public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, Guid>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<CreateOrderCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the CreateOrderCommandHandler class
    /// </summary>
    /// <param name="orderRepository">Order repository</param>
    /// <param name="publishEndpoint">MassTransit publish endpoint</param>
    /// <param name="logger">Logger instance</param>
    public CreateOrderCommandHandler(
        IOrderRepository orderRepository,
        IPublishEndpoint publishEndpoint,
        ILogger<CreateOrderCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    /// <summary>
    /// Handles the CreateOrderCommand
    /// </summary>
    /// <param name="request">The command to handle</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The ID of the created order</returns>
    public async Task<Guid> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling CreateOrderCommand for customer {CustomerName}", request.CustomerName);

        try
        {
            // Create the order entity from the command
            var order = new Order
            {
                CustomerId = request.CustomerId,
                CustomerName = request.CustomerName,
                CustomerEmail = request.CustomerEmail,
                Items = request.Items.Select(item => new OrderItem
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    UnitPrice = item.UnitPrice,
                    Quantity = item.Quantity
                }).ToList(),
                Status = OrderStatus.Created
            };

            // Save the order
            var createdOrder = await _orderRepository.CreateAsync(order);
            _logger.LogInformation("Order created with ID: {OrderId}", createdOrder.Id);

            // Publish the OrderCreatedEvent to start the saga
            var orderCreatedEvent = new OrderCreatedEvent
            {
                OrderId = createdOrder.Id,
                CustomerId = createdOrder.CustomerId,
                CustomerName = createdOrder.CustomerName,
                CustomerEmail = createdOrder.CustomerEmail,
                Items = createdOrder.Items.Select(item => new OrderItemDto
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    UnitPrice = item.UnitPrice,
                    Quantity = item.Quantity
                }).ToList(),
                TotalAmount = createdOrder.TotalAmount,
                CreatedAt = createdOrder.CreatedAt
            };

            await _publishEndpoint.Publish(orderCreatedEvent, cancellationToken);
            _logger.LogInformation("Published OrderCreatedEvent for order {OrderId}", createdOrder.Id);

            // Also publish the OrderCreated message for the NotificationService
            var orderCreatedMessage = new OrderCreated
            {
                OrderId = createdOrder.Id,
                CustomerName = createdOrder.CustomerName,
                CustomerEmail = createdOrder.CustomerEmail,
                TotalAmount = createdOrder.TotalAmount,
                ItemCount = createdOrder.Items.Count,
                CreatedAt = createdOrder.CreatedAt
            };

            await _publishEndpoint.Publish(orderCreatedMessage, cancellationToken);
            _logger.LogInformation("Published OrderCreated message for order {OrderId}", createdOrder.Id);

            return createdOrder.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order for customer {CustomerName}", request.CustomerName);
            throw;
        }
    }
}
