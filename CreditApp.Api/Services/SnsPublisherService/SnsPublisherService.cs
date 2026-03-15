using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using CreditApp.Domain.Entities;
using System.Text.Json;

namespace CreditApp.Api.Services.SnsPublisherService;

public class SnsPublisherService(IAmazonSimpleNotificationService snsClient, ILogger<SnsPublisherService> logger, IConfiguration configuration)
{
    private readonly string? _topicArn = configuration["AWS:SNS:TopicArn"];

    public async Task PublishCreditApplicationAsync(CreditApplication application, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_topicArn))
        {
            logger.LogWarning("SNS TopicArn не настроен, публикация пропущена");
            return;
        }

        try
        {
            var message = JsonSerializer.Serialize(application);

            var publishRequest = new PublishRequest
            {
                TopicArn = _topicArn,
                Message = message,
                Subject = $"CreditApplication-{application.Id}"
            };

            var response = await snsClient.PublishAsync(publishRequest, cancellationToken);

            logger.LogInformation(
                "Кредитная заявка {Id} опубликована в SNS, MessageId: {MessageId}",
                application.Id,
                response.MessageId);
        }
        catch (NotFoundException)
        {
            logger.LogWarning("Топик SNS не существует, попытка создать");

            try
            {
                var createTopicRequest = new CreateTopicRequest
                {
                    Name = "credit-applications"
                };

                var createResponse = await snsClient.CreateTopicAsync(createTopicRequest, cancellationToken);
                var createdTopicArn = createResponse.TopicArn;

                logger.LogInformation("Топик SNS создан: {TopicArn}", createdTopicArn);

                var message = JsonSerializer.Serialize(application);
                var publishRequest = new PublishRequest
                {
                    TopicArn = createdTopicArn,
                    Message = message,
                    Subject = $"CreditApplication-{application.Id}"
                };

                var response = await snsClient.PublishAsync(publishRequest, cancellationToken);

                logger.LogInformation(
                    "Кредитная заявка {Id} опубликована в SNS после создания топика, MessageId: {MessageId}",
                    application.Id,
                    response.MessageId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Не удалось создать топик SNS и опубликовать сообщение");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при публикации в SNS");
        }
    }
}
