using CreditApp.Api.Services.CreditGeneratorService;
using CreditApp.Api.Services.SnsPublisherService;
using CreditApp.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace CreditApp.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CreditController(CreditApplicationGeneratorService generatorService, SnsPublisherService snsPublisher, ILogger<CreditController> logger) : ControllerBase
{
    /// <summary>
    /// Получить кредитную заявку по ID, если не найдена в кэше генерируем новую
    /// </summary>
    /// <param name="id">ID кредитной заявки</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Кредитная заявка</returns>
    [HttpGet]
    public async Task<ActionResult<CreditApplication>> GetById([FromQuery] int id, CancellationToken cancellationToken)
    {
        logger.LogInformation("Получен запрос на получение/генерацию заявки {Id}", id);

        var (application, isNew) = await generatorService.GetByIdAsync(id, cancellationToken);

        if (isNew)
        {
            await snsPublisher.PublishCreditApplicationAsync(application, cancellationToken);
        }
        else
        {
            logger.LogInformation("Заявка {Id} получена из кэша, публикация в SNS пропущена", id);
        }

        return Ok(application);
    }
}
