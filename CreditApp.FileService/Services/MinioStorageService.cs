using CreditApp.FileService.Configuration;
using Minio;
using Minio.DataModel.Args;

namespace CreditApp.FileService.Services;

public class MinioStorageService(IMinioClient minioClient, MinioSettings settings, ILogger<MinioStorageService> logger)
{
    public async Task EnsureBucketExistsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var bucketExistsArgs = new BucketExistsArgs()
                .WithBucket(settings.BucketName);

            var found = await minioClient.BucketExistsAsync(bucketExistsArgs, cancellationToken);

            if (!found)
            {
                var makeBucketArgs = new MakeBucketArgs()
                    .WithBucket(settings.BucketName);

                await minioClient.MakeBucketAsync(makeBucketArgs, cancellationToken);
                logger.LogInformation("Bucket {BucketName} создан", settings.BucketName);
            }
            else
            {
                logger.LogInformation("Bucket {BucketName} уже существует", settings.BucketName);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при проверке/создании bucket {BucketName}", settings.BucketName);
            throw;
        }
    }

    public async Task<string> UploadFileAsync(string fileName, Stream fileStream, string contentType, CancellationToken cancellationToken = default)
    {
        try
        {
            var putObjectArgs = new PutObjectArgs()
                .WithBucket(settings.BucketName)
                .WithObject(fileName)
                .WithStreamData(fileStream)
                .WithObjectSize(fileStream.Length)
                .WithContentType(contentType);

            await minioClient.PutObjectAsync(putObjectArgs, cancellationToken);

            logger.LogInformation(
                "Файл {FileName} успешно загружен в bucket {BucketName}",
                fileName,
                settings.BucketName);

            return $"{settings.BucketName}/{fileName}";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при загрузке файла {FileName} в MinIO", fileName);
            throw;
        }
    }

    public async Task<List<string>> ListFilesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var files = new List<string>();
            var listArgs = new ListObjectsArgs()
                .WithBucket(settings.BucketName)
                .WithRecursive(true);

            await foreach (var item in minioClient.ListObjectsEnumAsync(listArgs, cancellationToken))
            {
                files.Add(item.Key);
            }

            logger.LogInformation("Получен список из {Count} файлов из bucket {BucketName}", files.Count, settings.BucketName);
            return files;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении списка файлов из bucket {BucketName}", settings.BucketName);
            throw;
        }
    }

    public async Task<string?> GetFileContentAsync(string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            string? content = null;
            var getArgs = new GetObjectArgs()
                .WithBucket(settings.BucketName)
                .WithObject(fileName)
                .WithCallbackStream(async (stream, ct) =>
                {
                    using var reader = new StreamReader(stream);
                    content = await reader.ReadToEndAsync(ct);
                });

            await minioClient.GetObjectAsync(getArgs, cancellationToken);

            logger.LogInformation("Файл {FileName} успешно получен из bucket {BucketName}", fileName, settings.BucketName);
            return content;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении файла {FileName} из bucket {BucketName}", fileName, settings.BucketName);
            return null;
        }
    }
}
