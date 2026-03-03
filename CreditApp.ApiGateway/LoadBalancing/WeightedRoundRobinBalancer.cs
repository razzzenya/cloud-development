using Ocelot.LoadBalancer.LoadBalancers;
using Ocelot.Responses;
using Ocelot.Values;

namespace CreditApp.ApiGateway.LoadBalancing;

/// <summary>
/// Weighted Round Robin балансировщик нагрузки для Ocelot.
/// </summary>
public class WeightedRoundRobinLoadBalancer(Func<Task<List<Service>>> servicesProvider, Dictionary<string, double> hostPortWeights) : ILoadBalancer
{
    private int _currentIndex = 0;
    private int _remainingRequests = 0;
    private readonly object _lock = new();

    public async Task<Response<ServiceHostAndPort>> Lease(HttpContext httpContext)
    {
        var services = await servicesProvider();

        if (services == null || services.Count == 0)
        {
            return new ErrorResponse<ServiceHostAndPort>(
                new ServicesAreEmptyError("No services available"));
        }

        ServiceHostAndPort selectedService;
        double selectedWeight;
        int selectedIndex;

        lock (_lock)
        {
            if (_remainingRequests <= 0)
            {
                _currentIndex = (_currentIndex + 1) % services.Count;

                var service = services[_currentIndex];
                var hostPort = $"{service.HostAndPort.DownstreamHost}:{service.HostAndPort.DownstreamPort}";

                var weight = hostPortWeights.TryGetValue(hostPort, out var w) ? w : 1.0;
                _remainingRequests = (int)Math.Ceiling(weight);
            }

            _remainingRequests--;

            var currentService = services[_currentIndex];
            var currentHostPort = $"{currentService.HostAndPort.DownstreamHost}:{currentService.HostAndPort.DownstreamPort}";

            selectedService = currentService.HostAndPort;
            selectedWeight = hostPortWeights.TryGetValue(currentHostPort, out var currentWeight) ? currentWeight : 1.0;
            selectedIndex = _currentIndex;
        }
        return new OkResponse<ServiceHostAndPort>(selectedService);
    }

    public void Release(ServiceHostAndPort hostAndPort)
    {
    }
}