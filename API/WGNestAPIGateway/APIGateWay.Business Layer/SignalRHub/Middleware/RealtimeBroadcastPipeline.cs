using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.SignalRHub.Middleware
{
    public class RealtimeBroadcastPipeline
    {
        private readonly List<IBroadcastRouteEntry> _entries = new();

        internal IReadOnlyList<IBroadcastRouteEntry> Entries => _entries;

        /// <summary>
        /// Register a route that should trigger a real-time broadcast when it succeeds.
        /// </summary>
        /// <typeparam name="TDto">The response DTO type the endpoint returns.</typeparam>
        /// <param name="method">HTTP method: "POST", "PUT", "DELETE", "PATCH"</param>
        /// <param name="routePattern">
        ///   The API path. Supports {param} placeholders.
        ///   Example: "/api/Ticket/UpdateTicket/{id}"
        /// </param>
        /// <param name="action">RealtimeActions.Create / Update / Delete</param>
        /// <param name="config">Entry from RealtimeBroadcastRegistry</param>
        public RealtimeBroadcastPipeline Register<TDto>(
            string method,
            string routePattern,
            string action,
            BroadcastEntityConfig<TDto> config) where TDto : class
        {
            _entries.Add(new BroadcastRouteEntry<TDto>(method, routePattern, action, config));
            return this; // fluent
        }
    }
}
