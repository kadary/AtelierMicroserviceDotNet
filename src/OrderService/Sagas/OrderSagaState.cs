using System;
using System.Collections.Generic;
using MassTransit;
using OrderService.CQRS.DTOs;
using OrderService.Models;

namespace OrderService.Sagas;

/// <summary>
/// State for the OrderSaga
/// </summary>
public class OrderSagaState : SagaStateMachineInstance, ISaga
{
    /// <summary>
    /// Correlation ID for the saga (Order ID)
    /// </summary>
    public Guid CorrelationId { get; set; }

    /// <summary>
    /// Current state of the saga
    /// </summary>
    public string CurrentState { get; set; } = string.Empty;

    /// <summary>
    /// Customer identifier
    /// </summary>
    public Guid CustomerId { get; set; }

    /// <summary>
    /// Customer name
    /// </summary>
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>
    /// Customer email for notifications
    /// </summary>
    public string CustomerEmail { get; set; } = string.Empty;

    /// <summary>
    /// List of items in the order
    /// </summary>
    public List<OrderItemDto> OrderItems { get; set; } = new();

    /// <summary>
    /// Total amount of the order
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Date and time when the order was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Flag indicating if products have been reserved
    /// </summary>
    public bool ProductsReserved { get; set; }

    /// <summary>
    /// Flag indicating if notification has been sent
    /// </summary>
    public bool NotificationSent { get; set; }

    /// <summary>
    /// Error message if any step fails
    /// </summary>
    public string? ErrorMessage { get; set; }
}
