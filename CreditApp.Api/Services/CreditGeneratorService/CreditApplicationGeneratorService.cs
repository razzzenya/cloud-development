using Bogus;
using CreditApp.Domain.Entities;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace CreditApp.Api.Services.CreditGeneratorService;

public class CreditApplicationGeneratorService(IDistributedCache cache, IConfiguration configuration, ILogger<CreditApplicationGeneratorService> logger, IHttpClientFactory httpClientFactory)
{
    private static readonly string[] _creditTypes =
    [
        "Потребительский",
        "Ипотека",
        "Автокредит",
        "Бизнес-кредит",
        "Образовательный"
    ];

    private static readonly string[] _statuses =
    [
        "Новая",
        "В обработке",
        "Одобрена",
        "Отклонена"
    ];

    private static readonly string[] _terminalStatuses = ["Одобрена", "Отклонена"];

    private readonly int _expirationMinutes = configuration.GetValue("CacheSettings:ExpirationMinutes", 10);
    private readonly string? _fileServiceUrl = configuration["FileService:Url"];

    public async Task<(CreditApplication Application, bool IsNew)> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = $"credit-application-{id}";

            logger.LogInformation("Попытка получить заявку {Id} из кэша", id);

            var cachedData = await cache.GetStringAsync(cacheKey, cancellationToken);

            if (!string.IsNullOrEmpty(cachedData))
            {
                var deserializedApplication = JsonSerializer.Deserialize<CreditApplication>(cachedData);

                if (deserializedApplication != null)
                {
                    logger.LogInformation("Заявка {Id} найдена в кэше", id);
                    return (deserializedApplication, IsNew: false);
                }

                logger.LogWarning("Заявка {Id} найдена в кэше, но не удалось десериализовать", id);
            }

            logger.LogInformation("Заявка {Id} не найдена в кэше, проверяем MinIO", id);

            var applicationFromStorage = await TryGetFromStorageAsync(id, cancellationToken);

            if (applicationFromStorage != null)
            {
                logger.LogInformation("Заявка {Id} найдена в MinIO, кэшируем", id);

                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_expirationMinutes)
                };

                await cache.SetStringAsync(
                    cacheKey,
                    JsonSerializer.Serialize(applicationFromStorage),
                    cacheOptions,
                    cancellationToken);

                return (applicationFromStorage, IsNew: false);
            }

            logger.LogInformation("Заявка {Id} не найдена в хранилище, генерируем новую", id);

            var application = GenerateApplication(id);

            var newCacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_expirationMinutes)
            };

            await cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(application),
                newCacheOptions,
                cancellationToken);

            logger.LogInformation(
                "Кредитная заявка сгенерирована и закэширована: Id={Id}, Тип={Type}, Сумма={Amount}, Статус={Status}",
                application.Id,
                application.Type,
                application.Amount,
                application.Status);

            return (application, IsNew: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении/генерации заявки {Id}", id);
            throw;
        }
    }

    private async Task<CreditApplication?> TryGetFromStorageAsync(int id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_fileServiceUrl))
        {
            logger.LogWarning("FileService URL не настроен, пропускаем проверку хранилища");
            return null;
        }

        try
        {
            var httpClient = httpClientFactory.CreateClient();

            var filesResponse = await httpClient.GetAsync($"{_fileServiceUrl}/api/files", cancellationToken);

            if (!filesResponse.IsSuccessStatusCode)
            {
                logger.LogWarning("Не удалось получить список файлов из FileService: {StatusCode}", filesResponse.StatusCode);
                return null;
            }

            var filesJson = await filesResponse.Content.ReadAsStringAsync(cancellationToken);
            var files = JsonSerializer.Deserialize<List<string>>(filesJson);

            var matchingFile = files?.FirstOrDefault(f => f.Contains($"credit-application-{id}-"));

            if (matchingFile == null)
            {
                logger.LogInformation("Файл для заявки {Id} не найден в MinIO", id);
                return null;
            }

            var fileResponse = await httpClient.GetAsync($"{_fileServiceUrl}/api/files/{matchingFile}", cancellationToken);

            if (!fileResponse.IsSuccessStatusCode)
            {
                logger.LogWarning("Не удалось получить файл {FileName} из FileService", matchingFile);
                return null;
            }

            var fileContent = await fileResponse.Content.ReadAsStringAsync(cancellationToken);
            var application = JsonSerializer.Deserialize<CreditApplication>(fileContent);

            return application;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении заявки {Id} из хранилища", id);
            return null;
        }
    }

    /// <summary>
    /// Генерация кредитной заявки с указанным ID
    /// </summary>
    private static CreditApplication GenerateApplication(int id)
    {
        var faker = new Faker<CreditApplication>("ru")
            .RuleFor(c => c.Id, f => id)
            .RuleFor(c => c.Type, f => f.PickRandom(_creditTypes))
            .RuleFor(c => c.Amount, f => Math.Round(f.Finance.Amount(10000, 10000000), 2))
            .RuleFor(c => c.Term, f => f.Random.Int(6, 360))
            .RuleFor(c => c.InterestRate, f => Math.Round(f.Random.Double(16.0, 25.0), 2))
            .RuleFor(c => c.SubmissionDate, f => f.Date.PastDateOnly(2))
            .RuleFor(c => c.RequiresInsurance, f => f.Random.Bool())
            .RuleFor(c => c.Status, f => f.PickRandom(_statuses))
            .RuleFor(c => c.ApprovalDate, (f, c) =>
            {
                if (!_terminalStatuses.Contains(c.Status))
                    return null;

                return f.Date.BetweenDateOnly(c.SubmissionDate, DateOnly.FromDateTime(DateTime.Today));
            })
            .RuleFor(c => c.ApprovedAmount, (f, c) =>
            {
                if (c.Status != "Одобрена")
                    return null;

                return Math.Round(c.Amount * f.Random.Decimal(0.7m, 1.0m), 2);
            });

        return faker.Generate();
    }
}
