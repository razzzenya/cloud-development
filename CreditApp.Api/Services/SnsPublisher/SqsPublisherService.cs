using Amazon.SQS;
using Amazon.SQS.Model;
using CreditApp.Domain.Entities;
using System.Text.Json;

namespace CreditApp.Api.Services.SnsPublisher;

public class SqsPublisherService(IAmazonSQS sqsClient, ILogger<SqsPublisherService> logger, IConfiguration configuration)
{
    private readonly string? _queueUrl = configuration["AWS:SQS:QueueUrl"];

    public async Task PublishCreditApplicationAsync(CreditApplication application, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_queueUrl))
        {
            logger.LogWarning("SQS QueueUrl не настроен, публикация пропущена");
            return;
        }

        try
        {
            var message = JsonSerializer.Serialize(application);

            var request = new SendMessageRequest
            {
                QueueUrl = _queueUrl,
                MessageBody = message
            };

            var response = await sqsClient.SendMessageAsync(request, cancellationToken);

            logger.LogInformation(
                "Кредитная заявка {Id} опубликована в SQS, MessageId: {MessageId}",
                application.Id,
                response.MessageId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при публикации в SQS");
        }
    }
}
