using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using OrderService.Models;
using OrderService.Repositories;
using OrderService.Sagas.Events;

namespace OrderService.Sagas;

/// <summary>
/// State machine for the OrderSaga
/// </summary>
public class OrderSaga : MassTransitStateMachine<OrderSagaState>
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<OrderSaga> _logger;

    /// <summary>
    /// Initializes a new instance of the OrderSaga class
    /// </summary>
    public OrderSaga(
        IHttpClientFactory httpClientFactory,
        IOrderRepository orderRepository,
        ILogger<OrderSaga> logger)
    {
        _httpClientFactory = httpClientFactory;
        _orderRepository = orderRepository;
        _logger = logger;

        // Define the state machine
        InstanceState(x => x.CurrentState);

        // Define the correlation ID
        Event(() => OrderCreated, x => x.CorrelateById(context => context.Message.OrderId));

        // Define the initial state
        Initially(
            When(OrderCreated)
                .Then(context => InitializeState(context))
                .ThenAsync(context => ReserveProducts(context))
                .TransitionTo(ProductsReservationAttempted)
        );

        // Define the ProductsReservationAttempted state
        During(ProductsReservationAttempted,
            When(ProductsReservationSucceeded)
                .Then(context => context.Instance.ProductsReserved = true)
                .ThenAsync(context => SendNotification(context))
                .TransitionTo(NotificationAttempted),
            When(ProductsReservationFailed)
                .Then(context => HandleProductsReservationFailure(context))
                .TransitionTo(OrderFailed)
        );

        // Define the NotificationAttempted state
        During(NotificationAttempted,
            When(NotificationSucceeded)
                .Then(context => context.Instance.NotificationSent = true)
                .ThenAsync(context => CompleteOrder(context))
                .TransitionTo(OrderCompleted),
            When(NotificationFailed)
                .Then(context => HandleNotificationFailure(context))
                .TransitionTo(OrderFailed)
        );

        // Define the OrderFailed state
        During(OrderFailed,
            When(CompensationSucceeded)
                .TransitionTo(OrderCancelled)
        );
    }

    // Define the events
    public Event<OrderCreatedEvent> OrderCreated { get; private set; }
    public Event<ProductsReservationSucceededEvent> ProductsReservationSucceeded { get; private set; }
    public Event<ProductsReservationFailedEvent> ProductsReservationFailed { get; private set; }
    public Event<NotificationSucceededEvent> NotificationSucceeded { get; private set; }
    public Event<NotificationFailedEvent> NotificationFailed { get; private set; }
    public Event<CompensationSucceededEvent> CompensationSucceeded { get; private set; }

    // Define the states
    public State ProductsReservationAttempted { get; private set; }
    public State NotificationAttempted { get; private set; }
    public State OrderCompleted { get; private set; }
    public State OrderFailed { get; private set; }
    public State OrderCancelled { get; private set; }

    // Initialize the saga state
    private void InitializeState(BehaviorContext<OrderSagaState, OrderCreatedEvent> context)
    {
        var message = context.Message;
        var instance = context.Instance;

        instance.CorrelationId = message.OrderId;
        instance.CustomerId = message.CustomerId;
        instance.CustomerName = message.CustomerName;
        instance.CustomerEmail = message.CustomerEmail;
        instance.OrderItems = message.Items.ToList();
        instance.TotalAmount = message.TotalAmount;
        instance.CreatedAt = message.CreatedAt;
        instance.ProductsReserved = false;
        instance.NotificationSent = false;

        _logger.LogInformation("OrderSaga initialized for order {OrderId}", instance.CorrelationId);
    }

    // Reserve products
    private async Task ReserveProducts(BehaviorContext<OrderSagaState, OrderCreatedEvent> context)
    {
        var instance = context.Instance;
        _logger.LogInformation("Attempting to reserve products for order {OrderId}", instance.CorrelationId);

        try
        {
            // Create the ReserveProductsCommand
            var command = new ReserveProductsCommand
            {
                OrderId = instance.CorrelationId,
                Items = instance.OrderItems.Select(item => new ProductReservationItem
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity
                }).ToList()
            };

            // Send the command to the ProductService
            var client = _httpClientFactory.CreateClient("ProductService");
            var response = await client.PostAsJsonAsync("/api/products/reserve", command);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Products reserved successfully for order {OrderId}", instance.CorrelationId);
                await context.Raise(new ProductsReservationSucceededEvent { OrderId = instance.CorrelationId });
            }
            else
            {
                var errorMessage = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to reserve products for order {OrderId}: {ErrorMessage}", instance.CorrelationId, errorMessage);
                await context.Raise(new ProductsReservationFailedEvent 
                { 
                    OrderId = instance.CorrelationId,
                    ErrorMessage = errorMessage
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reserving products for order {OrderId}", instance.CorrelationId);
            await context.Raise(new ProductsReservationFailedEvent 
            { 
                OrderId = instance.CorrelationId,
                ErrorMessage = ex.Message
            });
        }
    }

    // Send notification
    private async Task SendNotification(BehaviorContext<OrderSagaState, ProductsReservationSucceededEvent> context)
    {
        var instance = context.Instance;
        _logger.LogInformation("Products reserved successfully for order {OrderId}, notification already handled by NotificationService", instance.CorrelationId);
        
        // The notification is already handled by the NotificationService consuming the OrderCreated message
        // So we just need to simulate the notification success event
        await context.Raise(new NotificationSucceededEvent { OrderId = instance.CorrelationId });
    }

    // Complete the order
    private async Task CompleteOrder(BehaviorContext<OrderSagaState, NotificationSucceededEvent> context)
    {
        var instance = context.Instance;
        _logger.LogInformation("Completing order {OrderId}", instance.CorrelationId);

        try
        {
            // Update the order status to Processed
            var order = await _orderRepository.GetByIdAsync(instance.CorrelationId);
            if (order != null)
            {
                await _orderRepository.UpdateStatusAsync(instance.CorrelationId, OrderStatus.Processed);
                _logger.LogInformation("Order {OrderId} completed successfully", instance.CorrelationId);
            }
            else
            {
                _logger.LogWarning("Order {OrderId} not found when completing", instance.CorrelationId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing order {OrderId}", instance.CorrelationId);
        }
    }

    // Handle products reservation failure
    private void HandleProductsReservationFailure(BehaviorContext<OrderSagaState, ProductsReservationFailedEvent> context)
    {
        var instance = context.Instance;
        var message = context.Message;

        instance.ErrorMessage = message.ErrorMessage;
        _logger.LogWarning("Products reservation failed for order {OrderId}: {ErrorMessage}", instance.CorrelationId, message.ErrorMessage);

        // No need to compensate as no products were reserved
        CancelOrder(instance.CorrelationId);
    }

    // Handle notification failure
    private void HandleNotificationFailure(BehaviorContext<OrderSagaState, NotificationFailedEvent> context)
    {
        var instance = context.Instance;
        var message = context.Message;

        instance.ErrorMessage = message.ErrorMessage;
        _logger.LogWarning("Notification failed for order {OrderId}: {ErrorMessage}", instance.CorrelationId, message.ErrorMessage);

        // Need to compensate by releasing the reserved products
        CompensateProductsReservation(instance.CorrelationId, instance.OrderItems);
    }

    // Cancel the order
    private async void CancelOrder(Guid orderId)
    {
        _logger.LogInformation("Cancelling order {OrderId}", orderId);

        try
        {
            // Update the order status to Cancelled
            await _orderRepository.UpdateStatusAsync(orderId, OrderStatus.Cancelled);
            _logger.LogInformation("Order {OrderId} cancelled", orderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling order {OrderId}", orderId);
        }
    }

    // Compensate products reservation
    private async void CompensateProductsReservation(Guid orderId, System.Collections.Generic.List<CQRS.DTOs.OrderItemDto> items)
    {
        _logger.LogInformation("Compensating products reservation for order {OrderId}", orderId);

        try
        {
            // Create the ReleaseProductsCommand
            var command = new ReleaseProductsCommand
            {
                OrderId = orderId,
                Items = items.Select(item => new ProductReservationItem
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity
                }).ToList()
            };

            // Send the command to the ProductService
            var client = _httpClientFactory.CreateClient("ProductService");
            var response = await client.PostAsJsonAsync("/api/products/release", command);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Products released successfully for order {OrderId}", orderId);
            }
            else
            {
                var errorMessage = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to release products for order {OrderId}: {ErrorMessage}", orderId, errorMessage);
            }

            // Cancel the order regardless of whether the products were released
            CancelOrder(orderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing products for order {OrderId}", orderId);
            // Cancel the order regardless of whether the products were released
            CancelOrder(orderId);
        }
    }
}

// Additional event classes for the saga
public class ProductsReservationSucceededEvent
{
    public Guid OrderId { get; set; }
}

public class ProductsReservationFailedEvent
{
    public Guid OrderId { get; set; }
    public string ErrorMessage { get; set; }
}

public class NotificationSucceededEvent
{
    public Guid OrderId { get; set; }
}

public class NotificationFailedEvent
{
    public Guid OrderId { get; set; }
    public string ErrorMessage { get; set; }
}

public class CompensationSucceededEvent
{
    public Guid OrderId { get; set; }
}

// Additional command class for compensation
public class ReleaseProductsCommand
{
    public Guid OrderId { get; set; }
    public System.Collections.Generic.List<ProductReservationItem> Items { get; set; } = new();
}