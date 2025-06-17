namespace NotificationService.Services;

/// <summary>
/// Implementation of the email service that simulates sending emails
/// </summary>
public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;

    /// <summary>
    /// Initializes a new instance of the EmailService class
    /// </summary>
    /// <param name="logger">Logger instance</param>
    public EmailService(ILogger<EmailService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<bool> SendOrderConfirmationAsync(
        string to, 
        string subject, 
        string customerName, 
        Guid orderId, 
        decimal totalAmount, 
        int itemCount)
    {
        _logger.LogInformation("Sending order confirmation email to {Email} for order {OrderId}", to, orderId);
        
        // In a real application, this would connect to an SMTP server or email service
        // For this demo, we'll just log the email details
        
        var emailContent = $@"
Dear {customerName},

Thank you for your order!

Order Details:
- Order ID: {orderId}
- Total Amount: ${totalAmount:F2}
- Items: {itemCount}

Your order has been received and is being processed.

Best regards,
The Store Team
";

        _logger.LogInformation("Email content: {EmailContent}", emailContent);
        _logger.LogInformation("Order confirmation email sent successfully to {Email} for order {OrderId}", to, orderId);
        
        // Simulate successful email sending
        return Task.FromResult(true);
    }
}