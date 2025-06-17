namespace NotificationService.Services;

/// <summary>
/// Interface for email notification service
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends an order confirmation email
    /// </summary>
    /// <param name="to">Recipient email address</param>
    /// <param name="subject">Email subject</param>
    /// <param name="customerName">Customer name</param>
    /// <param name="orderId">Order ID</param>
    /// <param name="totalAmount">Total order amount</param>
    /// <param name="itemCount">Number of items in the order</param>
    /// <returns>True if the email was sent successfully, false otherwise</returns>
    Task<bool> SendOrderConfirmationAsync(
        string to, 
        string subject, 
        string customerName, 
        Guid orderId, 
        decimal totalAmount, 
        int itemCount);
}