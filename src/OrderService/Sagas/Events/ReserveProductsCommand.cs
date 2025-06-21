using System;
using System.Collections.Generic;
using OrderService.CQRS.Commands;

namespace OrderService.Sagas.Events;

/// <summary>
/// Command to reserve products for an order
/// </summary>
public class ReserveProductsCommand
{
    /// <summary>
    /// The ID of the order
    /// </summary>
    public Guid OrderId { get; set; }
    
    /// <summary>
    /// List of products to reserve
    /// </summary>
    public List<ProductReservationItem> Items { get; set; } = new();
}

/// <summary>
/// Represents a product to reserve
/// </summary>
public class ProductReservationItem
{
    /// <summary>
    /// Product identifier
    /// </summary>
    public Guid ProductId { get; set; }
    
    /// <summary>
    /// Quantity to reserve
    /// </summary>
    public int Quantity { get; set; }
}