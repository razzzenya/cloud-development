var builder = DistributedApplication.CreateBuilder(args);

var minioAccessKey = builder.Configuration["MinIO:AccessKey"]!;
var minioSecretKey = builder.Configuration["MinIO:SecretKey"]!;

var redis = builder.AddRedis("cache")
    .WithRedisCommander();

var minio = builder.AddContainer("minio", "minio/minio")
    .WithEnvironment("MINIO_ROOT_USER", minioAccessKey)
    .WithEnvironment("MINIO_ROOT_PASSWORD", minioSecretKey)
    .WithArgs("server", "/data", "--console-address", ":9001")
    .WithEndpoint(port: 9000, targetPort: 9000, name: "api")
    .WithEndpoint(port: 9001, targetPort: 9001, name: "console")
    .WithBindMount("minio-data", "/data");

var localstack = builder.AddContainer("localstack", "localstack/localstack")
    .WithEnvironment("SERVICES", "sns")
    .WithEnvironment("DEBUG", "1")
    .WithEnvironment("AWS_ACCESS_KEY_ID", "test")
    .WithEnvironment("AWS_SECRET_ACCESS_KEY", "test")
    .WithEnvironment("AWS_DEFAULT_REGION", "us-east-1")
    .WithEndpoint(port: 4566, targetPort: 4566, name: "gateway")
    .WithBindMount("localstack-data", "/var/lib/localstack")
    .WithBindMount(Path.Combine(builder.AppHostDirectory, "localstack-init.sh"), "/etc/localstack/init/ready.d/init-aws.sh");

var api0 = builder.AddProject<Projects.CreditApp_Api>("creditapp-api-0")
    .WithReference(redis)
    .WithEndpoint("https", endpoint => endpoint.Port = 7170)
    .WaitFor(redis)
    .WaitFor(localstack);

var api1 = builder.AddProject<Projects.CreditApp_Api>("creditapp-api-1")
    .WithReference(redis)
    .WithEndpoint("https", endpoint => endpoint.Port = 7171)
    .WaitFor(redis)
    .WaitFor(localstack);

var api2 = builder.AddProject<Projects.CreditApp_Api>("creditapp-api-2")
    .WithReference(redis)
    .WithEndpoint("https", endpoint => endpoint.Port = 7172)
    .WaitFor(redis)
    .WaitFor(localstack);

var gateway = builder.AddProject<Projects.CreditApp_ApiGateway>("creditapp-apigateway")
    .WithReference(api0)
    .WithReference(api1)
    .WithReference(api2)
    .WaitFor(api0)
    .WaitFor(api1)
    .WaitFor(api2);

var fileService = builder.AddProject<Projects.CreditApp_FileService>("creditapp-fileservice")
    .WithEndpoint("http", endpoint => endpoint.Port = 5100)
    .WaitFor(minio);

builder.AddProject<Projects.Client_Wasm>("client")
    .WithReference(gateway)
    .WaitFor(gateway);

builder.Build().Run();
