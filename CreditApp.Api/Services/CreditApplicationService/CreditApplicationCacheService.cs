using CreditApp.Api.Services.SnsPublisher;
using CreditApp.Domain.Entities;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace CreditApp.Api.Services.CreditApplicationService;

/// <summary>
/// Сервис кэширования кредитных заявок в Redis
/// </summary>
public class CreditApplicationCacheService(
    IDistributedCache cache,
    IConfiguration configuration,
    SqsPublisherService snsPublisher,
    ILogger<CreditApplicationCacheService> logger)
{
    private readonly int _expirationMinutes = configuration.GetValue("CacheSettings:ExpirationMinutes", 10);

    public async Task<CreditApplication?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = GetCacheKey(id);
            logger.LogInformation("Попытка получить заявку {Id} из кэша", id);

            var cachedData = await cache.GetStringAsync(cacheKey, cancellationToken);

            if (string.IsNullOrEmpty(cachedData))
            {
                logger.LogInformation("Заявка {Id} не найдена в кэше", id);
                return null;
            }

            var application = JsonSerializer.Deserialize<CreditApplication>(cachedData);

            if (application == null)
            {
                logger.LogWarning("Заявка {Id} найдена в кэше, но не удалось десериализовать", id);
                return null;
            }

            logger.LogInformation("Заявка {Id} найдена в кэше", id);
            return application;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении заявки {Id} из кэша", id);
            return null;
        }
    }

    /// <summary>
    /// Сохраняет существующую заявку в кэш (без публикации в SNS)
    /// </summary>
    public async Task SetAsync(CreditApplication application, CancellationToken cancellationToken = default)
    {
        await SetInternalAsync(application, cancellationToken);
    }

    /// <summary>
    /// Сохраняет новую заявку в кэш и публикует событие в SNS
    /// </summary>
    public async Task SetNewAsync(CreditApplication application, CancellationToken cancellationToken = default)
    {
        await SetInternalAsync(application, cancellationToken);

        await snsPublisher.PublishCreditApplicationAsync(application, cancellationToken);
        logger.LogInformation("Новая заявка {Id} опубликована в SNS", application.Id);
    }

    private async Task SetInternalAsync(CreditApplication application, CancellationToken cancellationToken)
    {
        try
        {
            var cacheKey = GetCacheKey(application.Id);
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_expirationMinutes)
            };

            await cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(application),
                cacheOptions,
                cancellationToken);

            logger.LogInformation("Заявка {Id} сохранена в кэш на {Minutes} минут", application.Id, _expirationMinutes);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при сохранении заявки {Id} в кэш", application.Id);
            throw;
        }
    }

    private static string GetCacheKey(int id) => $"credit-application-{id}";
}
