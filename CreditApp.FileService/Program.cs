using CreditApp.FileService.Configuration;
using CreditApp.FileService.Services;
using CreditApp.ServiceDefaults;
using Minio;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var minioSettings = builder.Configuration.GetSection("MinIO").Get<MinioSettings>() ?? new MinioSettings();

builder.Services.AddSingleton(minioSettings);

builder.Services.AddSingleton(sp =>
{
    var settings = sp.GetRequiredService<MinioSettings>();
    var logger = sp.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("MinIO Endpoint from config: '{Endpoint}'", settings.Endpoint ?? "(null)");

    string endpoint;
    var endpointValue = settings.Endpoint?.Trim() ?? string.Empty;

    if (string.IsNullOrEmpty(endpointValue))
    {
        logger.LogError("MinIO Endpoint is null or empty!");
        throw new InvalidOperationException("MinIO Endpoint is not configured");
    }

    if (endpointValue.StartsWith("http://") || endpointValue.StartsWith("https://"))
    {
        var uri = new Uri(endpointValue);
        endpoint = $"{uri.Host}:{uri.Port}";
        logger.LogInformation("Parsed MinIO endpoint from URL: '{Endpoint}'", endpoint);
    }
    else
    {
        endpoint = endpointValue;
        logger.LogInformation("Using MinIO endpoint as-is: '{Endpoint}'", endpoint);
    }

    return new MinioClient()
        .WithEndpoint(endpoint)
        .WithCredentials(settings.AccessKey, settings.SecretKey)
        .WithSSL(settings.UseSSL)
        .Build();
});

builder.Services.AddScoped<MinioStorageService>();

builder.Services.AddSingleton(new JsonSerializerOptions { WriteIndented = true });

builder.Services.AddHttpClient();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
