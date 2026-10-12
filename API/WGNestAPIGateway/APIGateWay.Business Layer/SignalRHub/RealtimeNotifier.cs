using APIGateWay.ModalLayer.Hub;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace APIGateWay.BusinessLayer.SignalRHub
{
    public class RealtimeNotifier : IRealtimeNotifier
    {
        private readonly IHubContext<RealtimeHub> _hub;
        private readonly ILogger<RealtimeNotifier> _logger;

        public RealtimeNotifier(
            IHubContext<RealtimeHub> hub,
            ILogger<RealtimeNotifier> logger)
        {
            _hub = hub;
            _logger = logger;
        }

        public async Task BroadcastAsync(RealtimeMessage message)
        {
            var tasks = new List<Task>();

            // 1. Admins receive data updates (Tickets) to keep UI fresh, 
            // BUT they DO NOT receive personal Bell Notifications meant for others.
            if (message.Entity != "Notification")
            {
                tasks.Add(_hub.Clients
                    .Group("global-admin")
                    .SendAsync("EntityChanged", message));
            }

            // 2. Repo-scoped users
            if (!string.IsNullOrWhiteSpace(message.RepoKey))
            {
                // Ensure we don't accidentally send to "repo-repo-id"
                var groupName = message.RepoKey.StartsWith("repo-") ? message.RepoKey : $"repo-{message.RepoKey}";

                tasks.Add(_hub.Clients
                    .Group(groupName)
                    .SendAsync("EntityChanged", message));
            }

            // 3. Personal delivery (assignment / resourceIds notifications)
            if (message.TargetUserId.HasValue && message.TargetUserId != Guid.Empty)
            {
                // Target the exact user group registered in RealtimeHub.cs
                tasks.Add(_hub.Clients
                    .Group($"user-{message.TargetUserId.Value}")
                    .SendAsync("EntityChanged", message));
            }

            await Task.WhenAll(tasks);

            _logger.LogInformation(
                 "[RealtimeNotifier] Sent {Entity} {Action} → Repo: {RepoKey}, User: {User}",
                 message.Entity,
                 message.Action,
                 message.RepoKey ?? "none",
                 message.TargetUserId?.ToString() ?? "none"
             );
        }
    }
}