using CreditApp.ApiGateway.LoadBalancing;
using CreditApp.ServiceDefaults;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

var generatorNames = builder.Configuration.GetSection("GeneratorServices").Get<string[]>() ?? [];
var serviceWeights = builder.Configuration
    .GetSection("ReplicaWeights")
    .Get<Dictionary<string, double>>() ?? [];

var addressOverrides = new List<KeyValuePair<string, string?>>();
var hostPortWeights = new Dictionary<string, double>();

for (var i = 0; i < generatorNames.Length; i++)
{
    var name = generatorNames[i];
    var url = builder.Configuration[$"services:{name}:https:0"];

    string resolvedHost, resolvedPort;
    if (!string.IsNullOrEmpty(url) && Uri.TryCreate(url, UriKind.Absolute, out var uri))
    {
        resolvedHost = uri.Host;
        resolvedPort = uri.Port.ToString();
        addressOverrides.Add(new($"Routes:0:DownstreamHostAndPorts:{i}:Host", resolvedHost));
        addressOverrides.Add(new($"Routes:0:DownstreamHostAndPorts:{i}:Port", resolvedPort));
    }
    else
    {
        resolvedHost = builder.Configuration[$"Routes:0:DownstreamHostAndPorts:{i}:Host"] ?? "localhost";
        resolvedPort = builder.Configuration[$"Routes:0:DownstreamHostAndPorts:{i}:Port"] ?? "0";
    }

    if (serviceWeights.TryGetValue(name, out var weight))
    {
        hostPortWeights[$"{resolvedHost}:{resolvedPort}"] = weight;
    }
}

if (addressOverrides.Count > 0)
    builder.Configuration.AddInMemoryCollection(addressOverrides);

builder.Services
    .AddOcelot(builder.Configuration)
    .AddCustomLoadBalancer((serviceProvider, route, serviceDiscovery) =>
    {
        return new WeightedRoundRobinLoadBalancer(
            async () => await serviceDiscovery.GetAsync(),
            hostPortWeights);
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorWasm", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.MapDefaultEndpoints();
app.UseHealthChecks("/health");
app.UseHealthChecks("/alive", new HealthCheckOptions { Predicate = r => r.Tags.Contains("live") });
app.UseCors("AllowBlazorWasm");
await app.UseOcelot();

app.Run();