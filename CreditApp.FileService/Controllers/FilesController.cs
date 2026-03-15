using CreditApp.FileService.Services;
using Microsoft.AspNetCore.Mvc;

namespace CreditApp.FileService.Controllers;

[Route("api/[controller]")]
[ApiController]
public class FilesController(MinioStorageService minioStorage, ILogger<FilesController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<string>>> GetFilesList(CancellationToken cancellationToken)
    {
        try
        {
            await minioStorage.EnsureBucketExistsAsync(cancellationToken);
            var files = await minioStorage.ListFilesAsync(cancellationToken);
            return Ok(files);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении списка файлов");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("{fileName}")]
    public async Task<IActionResult> GetFile(string fileName, CancellationToken cancellationToken)
    {
        try
        {
            var content = await minioStorage.GetFileContentAsync(fileName, cancellationToken);

            if (content == null)
            {
                return NotFound(new { error = "File not found" });
            }

            return Content(content, "application/json");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении файла {FileName}", fileName);
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
