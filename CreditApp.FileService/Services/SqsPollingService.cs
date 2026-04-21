using Amazon.SQS;
using Amazon.SQS.Model;
using CreditApp.Domain.Entities;
using CreditApp.FileService.Services;
using System.Text.Json;

namespace CreditApp.FileService.Services;

public class SqsPollingService(
    IAmazonSQS sqsClient,
    MinioStorageService minioStorage,
    IConfiguration configuration,
    JsonSerializerOptions jsonOptions,
    ILogger<SqsPollingService> logger) : BackgroundService
{
    private readonly string? _queueUrl = configuration["AWS:SQS:QueueUrl"];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrEmpty(_queueUrl))
        {
            logger.LogWarning("SQS QueueUrl не настроен, polling не запущен");
            return;
        }

        logger.LogInformation("SQS polling запущен, очередь: {QueueUrl}", _queueUrl);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var request = new ReceiveMessageRequest
                {
                    QueueUrl = _queueUrl,
                    MaxNumberOfMessages = 10,
                    WaitTimeSeconds = 20
                };

                var response = await sqsClient.ReceiveMessageAsync(request, stoppingToken);

                foreach (var message in response.Messages)
                {
                    await ProcessMessageAsync(message, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при polling SQS");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private async Task ProcessMessageAsync(Message message, CancellationToken cancellationToken)
    {
        try
        {
            var creditApplication = JsonSerializer.Deserialize<CreditApplication>(message.Body);

            if (creditApplication == null)
            {
                logger.LogWarning("Не удалось десериализовать CreditApplication из сообщения {MessageId}", message.MessageId);
                return;
            }

            logger.LogInformation("Получена кредитная заявка {Id} из SQS", creditApplication.Id);

            await minioStorage.EnsureBucketExistsAsync(CancellationToken.None);

            var fileName = $"credit-application-{creditApplication.Id}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
            var jsonContent = JsonSerializer.Serialize(creditApplication, jsonOptions);

            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(jsonContent));

            var uploadedPath = await minioStorage.UploadFileAsync(fileName, stream, "application/json", CancellationToken.None);

            logger.LogInformation("Кредитная заявка {Id} сохранена: {Path}", creditApplication.Id, uploadedPath);

            await sqsClient.DeleteMessageAsync(_queueUrl, message.ReceiptHandle, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обработке сообщения {MessageId}", message.MessageId);
        }
    }
}
