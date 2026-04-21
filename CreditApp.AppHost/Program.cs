var builder = DistributedApplication.CreateBuilder(args);

var minioAccessKey = builder.Configuration["MinIO:AccessKey"]!;
var minioSecretKey = builder.Configuration["MinIO:SecretKey"]!;

var redis = builder.AddRedis("cache")
    .WithRedisCommander();

var minio = builder.AddContainer("minio", "minio/minio")
    .WithEnvironment("MINIO_ROOT_USER", minioAccessKey)
    .WithEnvironment("MINIO_ROOT_PASSWORD", minioSecretKey)
    .WithArgs("server", "/data", "--console-address", ":9001")
    .WithHttpEndpoint(port: 9000, targetPort: 9000, name: "api")
    .WithHttpEndpoint(port: 9001, targetPort: 9001, name: "console")
    .WithBindMount("minio-data", "/data");

var localstack = builder.AddContainer("localstack", "localstack/localstack")
    .WithEnvironment("SERVICES", "sqs")
    .WithEnvironment("DEBUG", "1")
    .WithEnvironment("AWS_ACCESS_KEY_ID", "test")
    .WithEnvironment("AWS_SECRET_ACCESS_KEY", "test")
    .WithEnvironment("AWS_DEFAULT_REGION", "us-east-1")
    .WithHttpEndpoint(port: 4566, targetPort: 4566, name: "gateway")
    .WithBindMount("localstack-data", "/var/lib/localstack")
    .WithBindMount(Path.Combine(builder.AppHostDirectory, "localstack-init.sh"), "/etc/localstack/init/ready.d/init-aws.sh");


var fileService = builder.AddProject<Projects.CreditApp_FileService>("creditapp-fileservice")
    .WithEnvironment(ctx =>
    {
        var minioEndpoint = minio.GetEndpoint("api");
        ctx.EnvironmentVariables["MinIO__Endpoint"] = $"{minioEndpoint.Host}:{minioEndpoint.Port}";
        var localstackEndpoint = localstack.GetEndpoint("gateway");
        ctx.EnvironmentVariables["AWS__ServiceURL"] = $"http://{localstackEndpoint.Host}:{localstackEndpoint.Port}";
        ctx.EnvironmentVariables["AWS__SQS__QueueUrl"] = $"http://{localstackEndpoint.Host}:{localstackEndpoint.Port}/000000000000/credit-applications";
    })
    .WithEnvironment("MinIO__AccessKey", minioAccessKey)
    .WithEnvironment("MinIO__SecretKey", minioSecretKey)
    .WithEndpoint("http", endpoint => endpoint.Port = 5100)
    .WithEndpoint("https", endpoint => endpoint.Port = 7143)
    .WaitFor(minio)
    .WaitFor(localstack);

var api0 = builder.AddProject<Projects.CreditApp_Api>("creditapp-api-0")
    .WithReference(redis)
    .WithReference(fileService)
    .WithEnvironment(ctx =>
    {
        var localstackEndpoint = localstack.GetEndpoint("gateway");
        ctx.EnvironmentVariables["AWS__ServiceURL"] = $"http://{localstackEndpoint.Host}:{localstackEndpoint.Port}";
        ctx.EnvironmentVariables["AWS__SQS__QueueUrl"] = $"http://{localstackEndpoint.Host}:{localstackEndpoint.Port}/000000000000/credit-applications";
    })
    .WithEndpoint("http", endpoint => endpoint.Port = 5179)
    .WithEndpoint("https", endpoint => endpoint.Port = 7170)
    .WaitFor(redis)
    .WaitFor(localstack);

var api1 = builder.AddProject<Projects.CreditApp_Api>("creditapp-api-1")
    .WithReference(redis)
    .WithReference(fileService)
    .WithEnvironment(ctx =>
    {
        var localstackEndpoint = localstack.GetEndpoint("gateway");
        ctx.EnvironmentVariables["AWS__ServiceURL"] = $"http://{localstackEndpoint.Host}:{localstackEndpoint.Port}";
        ctx.EnvironmentVariables["AWS__SQS__QueueUrl"] = $"http://{localstackEndpoint.Host}:{localstackEndpoint.Port}/000000000000/credit-applications";
    })
    .WithEndpoint("http", endpoint => endpoint.Port = 5180)
    .WithEndpoint("https", endpoint => endpoint.Port = 7171)
    .WaitFor(redis)
    .WaitFor(localstack);

var api2 = builder.AddProject<Projects.CreditApp_Api>("creditapp-api-2")
    .WithReference(redis)
    .WithReference(fileService)
    .WithEnvironment(ctx =>
    {
        var localstackEndpoint = localstack.GetEndpoint("gateway");
        ctx.EnvironmentVariables["AWS__ServiceURL"] = $"http://{localstackEndpoint.Host}:{localstackEndpoint.Port}";
        ctx.EnvironmentVariables["AWS__SQS__QueueUrl"] = $"http://{localstackEndpoint.Host}:{localstackEndpoint.Port}/000000000000/credit-applications";
    })
    .WithEndpoint("http", endpoint => endpoint.Port = 5181)
    .WithEndpoint("https", endpoint => endpoint.Port = 7172)
    .WaitFor(redis)
    .WaitFor(localstack);

var gateway = builder.AddProject<Projects.CreditApp_ApiGateway>("creditapp-apigateway")
    .WithReference(api0)
    .WithReference(api1)
    .WithReference(api2)
    .WithEndpoint("http", endpoint => endpoint.Port = 5062)
    .WithEndpoint("https", endpoint => endpoint.Port = 7138)
    .WaitFor(api0)
    .WaitFor(api1)
    .WaitFor(api2);

builder.AddProject<Projects.Client_Wasm>("client")
    .WithReference(gateway)
    .WithEndpoint("http", endpoint => endpoint.Port = 5080)
    .WithEndpoint("https", endpoint => endpoint.Port = 7080)
    .WaitFor(gateway);

builder.Build().Run();
