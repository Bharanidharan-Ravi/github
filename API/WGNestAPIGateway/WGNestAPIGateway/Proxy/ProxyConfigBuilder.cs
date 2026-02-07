using APIGateway.Config;
using Yarp.ReverseProxy.Configuration;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Transforms;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using Yarp.ReverseProxy.Transforms.Builder;

namespace APIGateway.Proxy
{
    public class ProxyConfigBuilder
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        // Constructor where IHttpContextAccessor is injected
        public ProxyConfigBuilder(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }
        public static (IReadOnlyList<RouteConfig> Routes, IReadOnlyList<ClusterConfig> Clusters) Build()
        {
            var routes = new List<RouteConfig>();
            var clusters = new List<ClusterConfig>();


            foreach (var service in ServiceRegistry.Services)
            {
                var routeId = $"{service.Key}Route";
                var clusterId = $"{service.Key}Cluster";
                // Define Cluster Configurations
                clusters.Add(new ClusterConfig
                {
                    ClusterId = clusterId,
                    Destinations = new Dictionary<string, DestinationConfig>
                    {
                        { $"{service.Key}Destination", new DestinationConfig { Address = service.Value } }
                    },
                });

                // Define Route Configuration
                routes.Add(new RouteConfig
                {
                    RouteId = routeId,
                    ClusterId = clusterId,
                    Match = new RouteMatch
                    {
                        Path = $"/{service.Key}/{{**catch-all}}"
                    }
                });
            }

            return (routes, clusters);
        }
    }
}
