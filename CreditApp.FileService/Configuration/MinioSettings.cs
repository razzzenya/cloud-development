namespace CreditApp.FileService.Configuration;

public class MinioSettings
{
    public string Endpoint { get; set; } = "localhost:9000";
    public string AccessKey { get; set; } = "minioadmin";
    public string SecretKey { get; set; } = "minioadmin";
    public bool UseSSL { get; set; } = false;
    public string BucketName { get; set; } = "credit-applications";
}
