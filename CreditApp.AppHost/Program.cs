var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("cache")
    .WithRedisCommander();

var api0 = builder.AddProject<Projects.CreditApp_Api>("creditapp-api-0")
    .WithReference(redis)
    .WithEndpoint("https", endpoint => endpoint.Port = 7170)
    .WaitFor(redis);

var api1 = builder.AddProject<Projects.CreditApp_Api>("creditapp-api-1")
    .WithReference(redis)
    .WithEndpoint("https", endpoint => endpoint.Port = 7171)
    .WaitFor(redis);

var api2 = builder.AddProject<Projects.CreditApp_Api>("creditapp-api-2")
    .WithReference(redis)
    .WithEndpoint("https", endpoint => endpoint.Port = 7172)
    .WaitFor(redis);

var gateway = builder.AddProject<Projects.CreditApp_ApiGateway>("creditapp-apigateway")
    .WithReference(api0)
    .WithReference(api1)
    .WithReference(api2);

builder.AddProject<Projects.Client_Wasm>("client")
    .WithReference(gateway)
    .WaitFor(gateway);

builder.Build().Run();
