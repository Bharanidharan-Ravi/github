using APIGateWay.Business_Layer.SignalRHub.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.BusinessLayer.SignalRHub.Middleware
{
    public static class RealtimeBroadcastExtensions
    {
        // ── Step 1: Add to services ───────────────────────────────────────────
        //
        // builder.Services.AddRealtimeBroadcast(RealtimePipelineConfig.Configure);
        //
        public static IServiceCollection AddRealtimeBroadcast(
            this IServiceCollection services,
            Action<RealtimeBroadcastPipeline> configure)
        {
            // Build the route table once at startup
            var pipeline = new RealtimeBroadcastPipeline();
            configure(pipeline);

            // Singleton — route table never changes at runtime
            services.AddSingleton(pipeline);

            // Scoped — one per request (needs HttpContext / DI scope)
            services.AddScoped<IRealtimeBroadcaster, RealtimeBroadcaster>();

            // SignalR infrastructure
            services.AddSingleton<IUserIdProvider, GuidUserIdProvider>();
            services.AddScoped<IRealtimeNotifier, RealtimeNotifier>();

            return services;
        }

        // ── Step 2: Add to pipeline ───────────────────────────────────────────
        //
        // app.UseRealtimeBroadcast();
        //
        // Place AFTER UseAuthentication + UseAuthorization so Context.User
        // is populated when the middleware runs.
        //
        public static IApplicationBuilder UseRealtimeBroadcast(
            this IApplicationBuilder app) =>
            app.UseMiddleware<RealtimeBroadcastMiddleware>();
    }
}
