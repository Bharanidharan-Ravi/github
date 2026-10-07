using APIGateWay.DomainLayer.Utilities;
using Microsoft.AspNetCore.Http;

namespace APIGateWay.BusinessLayer.SignalRHub
{
    /// <summary>
    /// Live and Test clients share one hub, so every group name is scoped by environment:
    /// a Test connection joins "test:user-{id}" and a Test request broadcasts to "test:…",
    /// which keeps Test events from reaching Live users (and vice versa).
    /// </summary>
    public static class RealtimeGroups
    {
        private const string TestPrefix = "test:";

        public static string For(bool isTest, string group) => isTest ? TestPrefix + group : group;

        public static string For(HttpRequest? request, string group) =>
            For(EnvironmentRoutingService.IsTestRequest(request), group);
    }
}
