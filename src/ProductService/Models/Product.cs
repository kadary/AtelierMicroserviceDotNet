namespace ProductService.Models;

/// <summary>
/// Represents a product in the system
/// </summary>
public class Product
{
    /// <summary>
    /// Unique identifier for the product
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Name of the product
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// Description of the product
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Price of the product
    /// </summary>
    public decimal Price { get; set; }
    
    /// <summary>
    /// Current stock quantity
    /// </summary>
    public int StockQuantity { get; set; }
    
    /// <summary>
    /// Date and time when the product was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}