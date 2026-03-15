using CreditApp.Domain.Entities;
using CreditApp.FileService.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace CreditApp.FileService.Controllers;

[Route("api/[controller]")]
[ApiController]
public class NotificationController(MinioStorageService minioStorage, IHttpClientFactory httpClientFactory, JsonSerializerOptions jsonOptions, ILogger<NotificationController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> ReceiveSnsNotification(CancellationToken cancellationToken)
    {
        try
        {
            using var reader = new StreamReader(Request.Body);
            var bodyContent = await reader.ReadToEndAsync(cancellationToken);

            logger.LogInformation("Получено SNS уведомление: {Body}", bodyContent);

            var body = JsonSerializer.Deserialize<JsonElement>(bodyContent);

            if (body.TryGetProperty("Type", out var typeElement))
            {
                var messageType = typeElement.GetString();

                if (messageType == "SubscriptionConfirmation")
                {
                    logger.LogInformation("Получено подтверждение подписки SNS");

                    if (body.TryGetProperty("SubscribeURL", out var subscribeUrlElement))
                    {
                        var subscribeUrl = subscribeUrlElement.GetString();

                        if (!string.IsNullOrEmpty(subscribeUrl))
                        {
                            logger.LogInformation("Подтверждение подписки через URL: {Url}", subscribeUrl);

                            using var httpClient = httpClientFactory.CreateClient();
                            var response = await httpClient.GetAsync(subscribeUrl, cancellationToken);

                            if (response.IsSuccessStatusCode)
                            {
                                logger.LogInformation("Подписка SNS успешно подтверждена");
                            }
                            else
                            {
                                logger.LogWarning("Не удалось подтвердить подписку SNS: {StatusCode}", response.StatusCode);
                            }
                        }
                    }

                    return Ok(new { message = "Subscription confirmed" });
                }

                if (messageType == "Notification")
                {
                    if (body.TryGetProperty("Message", out var messageElement))
                    {
                        var messageJson = messageElement.GetString();

                        if (string.IsNullOrEmpty(messageJson))
                        {
                            logger.LogWarning("Получено пустое сообщение от SNS");
                            return BadRequest("Empty message");
                        }

                        var creditApplication = JsonSerializer.Deserialize<CreditApplication>(messageJson);

                        if (creditApplication == null)
                        {
                            logger.LogWarning("Не удалось десериализовать CreditApplication");
                            return BadRequest("Invalid credit application data");
                        }

                        logger.LogInformation(
                            "Получена кредитная заявка {Id} через SNS",
                            creditApplication.Id);

                        await minioStorage.EnsureBucketExistsAsync(cancellationToken);

                        var fileName = $"credit-application-{creditApplication.Id}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
                        var jsonContent = JsonSerializer.Serialize(creditApplication, jsonOptions);

                        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(jsonContent));

                        var uploadedPath = await minioStorage.UploadFileAsync(
                            fileName,
                            stream,
                            "application/json",
                            cancellationToken);

                        logger.LogInformation(
                            "Кредитная заявка {Id} сохранена в MinIO: {Path}",
                            creditApplication.Id,
                            uploadedPath);

                        return Ok(new { message = "Credit application saved", path = uploadedPath });
                    }
                }
            }

            logger.LogWarning("Получено неизвестное SNS сообщение");
            return BadRequest("Unknown message type");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обработке SNS уведомления");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
