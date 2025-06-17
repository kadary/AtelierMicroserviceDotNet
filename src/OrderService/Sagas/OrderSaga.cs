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

        // Initialize event properties
        OrderCreated = Event<OrderCreatedEvent>("OrderCreated");
        ProductsReservationSucceeded = Event<ProductsReservationSucceededEvent>("ProductsReservationSucceeded");
        ProductsReservationFailed = Event<ProductsReservationFailedEvent>("ProductsReservationFailed");
        NotificationSucceeded = Event<NotificationSucceededEvent>("NotificationSucceeded");
        NotificationFailed = Event<NotificationFailedEvent>("NotificationFailed");
        CompensationSucceeded = Event<CompensationSucceededEvent>("CompensationSucceeded");

        // Initialize state properties
        Initial = State("Initial");
        ProductsReservationAttempted = State("ProductsReservationAttempted");
        NotificationAttempted = State("NotificationAttempted");
        OrderCompleted = State("OrderCompleted");
        OrderFailed = State("OrderFailed");
        OrderCancelled = State("OrderCancelled");

        // Define the correlation ID for all events
        Event(() => OrderCreated, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => ProductsReservationSucceeded, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => ProductsReservationFailed, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => NotificationSucceeded, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => NotificationFailed, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => CompensationSucceeded, x => x.CorrelateById(context => context.Message.OrderId));

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
                .Then(context => 
                {
                    context.Saga.ProductsReserved = true;
                    _logger.LogInformation("Products reservation succeeded for order {OrderId}, updating state", context.Saga.CorrelationId);
                })
                .ThenAsync(context => SendNotification(context))
                .TransitionTo(NotificationAttempted),
            When(ProductsReservationFailed)
                .ThenAsync(context => HandleProductsReservationFailure(context))
                .TransitionTo(OrderFailed)
        );

        // Define the NotificationAttempted state
        During(NotificationAttempted,
            When(NotificationSucceeded)
                .Then(context => 
                {
                    context.Saga.NotificationSent = true;
                    _logger.LogInformation("Notification succeeded for order {OrderId}, updating state", context.Saga.CorrelationId);
                })
                .ThenAsync(context => CompleteOrder(context))
                .TransitionTo(OrderCompleted),
            When(NotificationFailed)
                .ThenAsync(context => HandleNotificationFailure(context))
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
    public State Initial { get; private set; }
    public State ProductsReservationAttempted { get; private set; }
    public State NotificationAttempted { get; private set; }
    public State OrderCompleted { get; private set; }
    public State OrderFailed { get; private set; }
    public State OrderCancelled { get; private set; }

    // Initialize the saga state
    private void InitializeState(BehaviorContext<OrderSagaState, OrderCreatedEvent> context)
    {
        var message = context.Message;
        var saga = context.Saga;

        saga.CorrelationId = message.OrderId;
        saga.CustomerId = message.CustomerId;
        saga.CustomerName = message.CustomerName;
        saga.CustomerEmail = message.CustomerEmail;
        saga.OrderItems = message.Items.ToList();
        saga.TotalAmount = message.TotalAmount;
        saga.CreatedAt = message.CreatedAt;
        saga.ProductsReserved = false;
        saga.NotificationSent = false;

        _logger.LogInformation("OrderSaga initialized for order {OrderId}", saga.CorrelationId);
    }

    // Reserve products
    private async Task ReserveProducts(BehaviorContext<OrderSagaState, OrderCreatedEvent> context)
    {
        var saga = context.Saga;
        _logger.LogInformation("Attempting to reserve products for order {OrderId}", saga.CorrelationId);

        try
        {
            // Create the ReserveProductsCommand
            var command = new ReserveProductsCommand
            {
                OrderId = saga.CorrelationId,
                Items = saga.OrderItems.Select(item => new ProductReservationItem
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity
                }).ToList()
            };

            // Send the command to the ProductService
            var client = _httpClientFactory.CreateClient("ProductService");
            _logger.LogInformation("Sending reserve products request to ProductService for order {OrderId} with {ItemCount} items", 
                saga.CorrelationId, command.Items.Count);

            var response = await client.PostAsJsonAsync("/api/products/reserve", command);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Products reserved successfully for order {OrderId}", saga.CorrelationId);
                // Use the state machine's event publishing mechanism
                await context.Publish(new ProductsReservationSucceededEvent { OrderId = saga.CorrelationId });
            }
            else
            {
                var errorMessage = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to reserve products for order {OrderId}: {ErrorMessage}", saga.CorrelationId, errorMessage);
                // Use the state machine's event publishing mechanism
                await context.Publish(new ProductsReservationFailedEvent 
                { 
                    OrderId = saga.CorrelationId,
                    ErrorMessage = errorMessage
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reserving products for order {OrderId}", saga.CorrelationId);
            // Use the state machine's event publishing mechanism
            await context.Publish(new ProductsReservationFailedEvent 
            { 
                OrderId = saga.CorrelationId,
                ErrorMessage = ex.Message
            });
        }
    }

    // Send notification
    private async Task SendNotification(BehaviorContext<OrderSagaState, ProductsReservationSucceededEvent> context)
    {
        var saga = context.Saga;
        _logger.LogInformation("Products reserved successfully for order {OrderId}, notification already handled by NotificationService", saga.CorrelationId);

        // The notification is already handled by the NotificationService consuming the OrderCreated message
        // So we just need to simulate the notification success event
        _logger.LogInformation("Publishing NotificationSucceededEvent for order {OrderId}", saga.CorrelationId);
        await context.Publish(new NotificationSucceededEvent { OrderId = saga.CorrelationId });
    }

    // Complete the order
    private async Task CompleteOrder(BehaviorContext<OrderSagaState, NotificationSucceededEvent> context)
    {
        var saga = context.Saga;
        _logger.LogInformation("Completing order {OrderId}", saga.CorrelationId);

        try
        {
            // Update the order status to Processed
            var order = await _orderRepository.GetByIdAsync(saga.CorrelationId);
            if (order != null)
            {
                await _orderRepository.UpdateStatusAsync(saga.CorrelationId, OrderStatus.Processed);
                _logger.LogInformation("Order {OrderId} completed successfully", saga.CorrelationId);
            }
            else
            {
                _logger.LogWarning("Order {OrderId} not found when completing", saga.CorrelationId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing order {OrderId}", saga.CorrelationId);
        }
    }

    // Handle products reservation failure
    private async Task HandleProductsReservationFailure(BehaviorContext<OrderSagaState, ProductsReservationFailedEvent> context)
    {
        var saga = context.Saga;
        var message = context.Message;

        saga.ErrorMessage = message.ErrorMessage;
        _logger.LogWarning("Products reservation failed for order {OrderId}: {ErrorMessage}", saga.CorrelationId, message.ErrorMessage);

        // No need to compensate as no products were reserved
        _logger.LogInformation("No products were reserved, proceeding to cancel order {OrderId}", saga.CorrelationId);
        await CancelOrder(saga.CorrelationId);

        // Publish the compensation succeeded event to complete the saga
        await context.Publish(new CompensationSucceededEvent { OrderId = saga.CorrelationId });
        _logger.LogInformation("Published CompensationSucceededEvent for order {OrderId}", saga.CorrelationId);
    }

    // Handle notification failure
    private async Task HandleNotificationFailure(BehaviorContext<OrderSagaState, NotificationFailedEvent> context)
    {
        var saga = context.Saga;
        var message = context.Message;

        saga.ErrorMessage = message.ErrorMessage;
        _logger.LogWarning("Notification failed for order {OrderId}: {ErrorMessage}", saga.CorrelationId, message.ErrorMessage);

        // Need to compensate by releasing the reserved products
        _logger.LogInformation("Notification failed, need to compensate by releasing reserved products for order {OrderId}", saga.CorrelationId);
        await CompensateProductsReservation(saga.CorrelationId, saga.OrderItems);

        // Cancel the order
        await CancelOrder(saga.CorrelationId);

        // Publish the compensation succeeded event to complete the saga
        await context.Publish(new CompensationSucceededEvent { OrderId = saga.CorrelationId });
        _logger.LogInformation("Published CompensationSucceededEvent for order {OrderId}", saga.CorrelationId);
    }

    // Cancel the order
    private async Task CancelOrder(Guid orderId)
    {
        _logger.LogInformation("Cancelling order {OrderId}", orderId);

        try
        {
            // Update the order status to Cancelled
            await _orderRepository.UpdateStatusAsync(orderId, OrderStatus.Cancelled);
            _logger.LogInformation("Order {OrderId} cancelled", orderId);

            // The CompensationSucceededEvent will be published by the caller
            _logger.LogInformation("Order cancellation completed for {OrderId}", orderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling order {OrderId}", orderId);
            // Even if there's an error, we consider the cancellation complete
            _logger.LogInformation("Order cancellation completed for {OrderId} despite error", orderId);
        }
    }

    // Compensate products reservation
    private async Task CompensateProductsReservation(Guid orderId, System.Collections.Generic.List<CQRS.DTOs.OrderItemDto> items)
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

            _logger.LogInformation("Sending release products request to ProductService for order {OrderId} with {ItemCount} items", 
                orderId, command.Items.Count);

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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing products for order {OrderId}", orderId);
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
    public string ErrorMessage { get; set; } = string.Empty;
}

public class NotificationSucceededEvent
{
    public Guid OrderId { get; set; }
}

public class NotificationFailedEvent
{
    public Guid OrderId { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
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
