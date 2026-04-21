using Amazon.SQS;
using CreditApp.Api.Services.CreditApplicationService;
using CreditApp.Api.Services.SnsPublisher;
using CreditApp.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddRedisDistributedCache("cache");

var awsOptions = new Amazon.Extensions.NETCore.Setup.AWSOptions
{
    DefaultClientConfig =
    {
        ServiceURL = builder.Configuration["AWS:ServiceURL"]
    }
};

builder.Services.AddDefaultAWSOptions(awsOptions);
builder.Services.AddAWSService<IAmazonSQS>();

builder.Services.AddScoped<SqsPublisherService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorWasm", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddHttpClient();

builder.Services.AddHttpClient("creditapp-fileservice", client =>
{
    client.BaseAddress = new Uri("http://creditapp-fileservice");
}).AddServiceDiscovery();

builder.Services.AddScoped<CreditApplicationGenerator>();
builder.Services.AddScoped<CreditApplicationCacheService>();
builder.Services.AddScoped<CreditApplicationStorageService>();
builder.Services.AddScoped<CreditApplicationService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "CreditApp API"
    });

    var xmlFilename = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    var domainXmlPath = Path.Combine(AppContext.BaseDirectory, "CreditApp.Domain.xml");
    if (File.Exists(domainXmlPath))
    {
        options.IncludeXmlComments(domainXmlPath);
    }
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowBlazorWasm");
app.MapControllers();
app.MapDefaultEndpoints();

app.Run();