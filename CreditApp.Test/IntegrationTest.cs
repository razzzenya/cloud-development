using Aspire.Hosting;
using Aspire.Hosting.Testing;
using CreditApp.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text.Json;

namespace CreditApp.Test;

public class AppHostFixture : IAsyncLifetime
{
    private static readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(60);
    public DistributedApplication? App { get; private set; }

    public async Task InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.CreditApp_AppHost>();

        appHost.Services.ConfigureHttpClientDefaults(http =>
            http.AddStandardResilienceHandler(options =>
            {
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(60);
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(120);
            }));

        App = await appHost.BuildAsync();
        await App.StartAsync();

        await App.ResourceNotifications.WaitForResourceHealthyAsync("cache").WaitAsync(_defaultTimeout);
        await App.ResourceNotifications.WaitForResourceHealthyAsync("creditapp-api-0").WaitAsync(_defaultTimeout);
        await App.ResourceNotifications.WaitForResourceHealthyAsync("creditapp-apigateway").WaitAsync(_defaultTimeout);
    }

    public async Task DisposeAsync()
    {
        if (App != null)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await App.DisposeAsync().AsTask().WaitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}

public class IntegrationTests(AppHostFixture fixture) : IClassFixture<AppHostFixture>
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task CreditApi_HealthCheck_ReturnsHealthy()
    {
        using var httpClient = fixture.App!.CreateHttpClient("creditapp-api-0");
        using var response = await httpClient.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task FileService_HealthCheck_ReturnsHealthy()
    {
        using var httpClient = fixture.App!.CreateHttpClient("creditapp-fileservice");
        using var response = await httpClient.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreditApi_GetById_ReturnsValidCreditApplication()
    {
        using var httpClient = fixture.App!.CreateHttpClient("creditapp-api-0");
        using var response = await httpClient.GetAsync("/api/credit?id=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var creditApp = JsonSerializer.Deserialize<CreditApplication>(content, _jsonOptions);

        Assert.NotNull(creditApp);
        Assert.Equal(1, creditApp.Id);
        Assert.NotEmpty(creditApp.Type);
        Assert.NotEmpty(creditApp.Status);
        Assert.True(creditApp.Amount > 0);
    }

    [Fact]
    public async Task Redis_CachingWorks_ReturnsCachedData()
    {
        var testId = Random.Shared.Next(1, 100000);
        using var httpClient = fixture.App!.CreateHttpClient("creditapp-api-0");

        using var firstResponse = await httpClient.GetAsync($"/api/credit?id={testId}");
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var firstContent = await firstResponse.Content.ReadAsStringAsync();

        using var secondResponse = await httpClient.GetAsync($"/api/credit?id={testId}");
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var secondContent = await secondResponse.Content.ReadAsStringAsync();

        Assert.Equal(firstContent, secondContent);
    }

    [Fact]
    public async Task AllServices_StartSuccessfully_AndAreHealthy()
    {
        var apiClient = fixture.App!.CreateHttpClient("creditapp-api-0");
        var fileServiceClient = fixture.App!.CreateHttpClient("creditapp-fileservice");

        using var apiHealthResponse = await apiClient.GetAsync("/health");
        using var fileServiceHealthResponse = await fileServiceClient.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, apiHealthResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, fileServiceHealthResponse.StatusCode);
    }

    [Fact]
    public async Task Gateway_GetCreditApplication_ReturnsValidData()
    {
        var testId = Random.Shared.Next(1, 100000);
        using var httpClient = fixture.App!.CreateHttpClient("creditapp-apigateway");

        using var response = await httpClient.GetAsync($"/api/credit?id={testId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var creditApp = JsonSerializer.Deserialize<CreditApplication>(content, _jsonOptions);

        Assert.NotNull(creditApp);
        Assert.Equal(testId, creditApp.Id);
        Assert.NotEmpty(creditApp.Type);
        Assert.NotEmpty(creditApp.Status);
        Assert.True(creditApp.Amount > 0);
    }

    [Fact]
    public async Task Gateway_RepeatedRequests_ReturnsCachedResponse()
    {
        var testId = Random.Shared.Next(1, 100000);
        using var httpClient = fixture.App!.CreateHttpClient("creditapp-apigateway");

        using var response1 = await httpClient.GetAsync($"/api/credit?id={testId}");
        response1.EnsureSuccessStatusCode();
        var content1 = await response1.Content.ReadAsStringAsync();

        using var response2 = await httpClient.GetAsync($"/api/credit?id={testId}");
        response2.EnsureSuccessStatusCode();
        var content2 = await response2.Content.ReadAsStringAsync();

        Assert.Equal(content1, content2);

        var creditApp1 = JsonSerializer.Deserialize<CreditApplication>(content1, _jsonOptions);
        var creditApp2 = JsonSerializer.Deserialize<CreditApplication>(content2, _jsonOptions);

        Assert.NotNull(creditApp1);
        Assert.NotNull(creditApp2);
        Assert.Equal(creditApp1.Id, creditApp2.Id);
        Assert.Equal(creditApp1.Type, creditApp2.Type);
        Assert.Equal(creditApp1.Amount, creditApp2.Amount);
    }
}
