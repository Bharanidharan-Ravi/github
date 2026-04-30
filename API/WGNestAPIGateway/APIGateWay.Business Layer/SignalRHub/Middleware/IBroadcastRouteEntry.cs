using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.SignalRHub.Middleware
{
    public interface IBroadcastRouteEntry
    {
        string HttpMethod { get; }
        string RoutePattern { get; }  // e.g. "/api/Ticket/UpdateTicket/{id}"
        string Action { get; }  // RealtimeActions.Create / Update / Delete

        /// <summary>
        /// Called by the middleware after it captures the response JSON.
        /// Deserializes to TDto and triggers the full broadcast pipeline.
        /// </summary>
        Task BroadcastAsync(string responseJson, IRealtimeBroadcaster broadcaster);
    }

    internal class BroadcastRouteEntry<TDto> : IBroadcastRouteEntry
        where TDto : class
    {
        public string HttpMethod { get; init; } = string.Empty;
        public string RoutePattern { get; init; } = string.Empty;
        public string Action { get; init; } = string.Empty;

        private readonly BroadcastEntityConfig<TDto> _config;

        public BroadcastRouteEntry(
            string httpMethod,
            string routePattern,
            string action,
            BroadcastEntityConfig<TDto> config)
        {
            HttpMethod = httpMethod.ToUpperInvariant();
            RoutePattern = routePattern;
            Action = action;
            _config = config;
        }

        public async Task BroadcastAsync(string responseJson, IRealtimeBroadcaster broadcaster)
        {
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            JsonElement payloadElement =
                root.TryGetProperty("Data", out var data) ? data :
                root.TryGetProperty("Result", out var result) ? result :
                root.TryGetProperty("Payload", out var payload) ? payload :
                root;

            var dto = payloadElement.Deserialize<TDto>();

            if (dto == null) return;

            await broadcaster.NotifyAsync(_config, Action, dto);
        }
    }
}
