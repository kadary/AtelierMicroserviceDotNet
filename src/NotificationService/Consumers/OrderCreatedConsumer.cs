namespace NotificationService.Consumers;

using MassTransit;
using NotificationService.Messages;
using NotificationService.Services;

/// <summary>
/// Consumer for OrderCreated events
/// </summary>
public class OrderCreatedConsumer : IConsumer<OrderCreated>
{
    private readonly IEmailService _emailService;
    private readonly ILogger<OrderCreatedConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the OrderCreatedConsumer class
    /// </summary>
    /// <param name="emailService">Email service</param>
    /// <param name="logger">Logger instance</param>
    public OrderCreatedConsumer(IEmailService emailService, ILogger<OrderCreatedConsumer> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    /// <summary>
    /// Consumes the OrderCreated message
    /// </summary>
    /// <param name="context">The consume context containing the message</param>
    public async Task Consume(ConsumeContext<OrderCreated> context)
    {
        var message = context.Message;
        
        _logger.LogInformation("Received OrderCreated event for order {OrderId}", message.OrderId);
        
        try
        {
            // Send order confirmation email
            var subject = $"Order Confirmation - Order #{message.OrderId}";
            
            var result = await _emailService.SendOrderConfirmationAsync(
                message.CustomerEmail,
                subject,
                message.CustomerName,
                message.OrderId,
                message.TotalAmount,
                message.ItemCount);
            
            if (result)
            {
                _logger.LogInformation("Order confirmation email sent successfully for order {OrderId}", message.OrderId);
            }
            else
            {
                _logger.LogWarning("Failed to send order confirmation email for order {OrderId}", message.OrderId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OrderCreated event for order {OrderId}", message.OrderId);
            // In a production environment, you might want to throw the exception to trigger the retry policy
            // or implement a dead-letter queue for failed messages
        }
    }
}