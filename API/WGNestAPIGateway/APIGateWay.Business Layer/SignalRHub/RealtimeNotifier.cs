using APIGateWay.ModalLayer.Hub;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;


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

            // 1. Admins always receive everything
            tasks.Add(_hub.Clients
                .Group("global-admin")
                .SendAsync("EntityChanged", message));

            // 2. Repo-scoped users
            if (!string.IsNullOrWhiteSpace(message.RepoKey))
            {
                tasks.Add(_hub.Clients
                    .Group($"repo-{message.RepoKey}")
                    .SendAsync("EntityChanged", message));
            }

            // 3. Personal delivery (assignment / mention notifications)
            if (message.TargetUserId.HasValue && message.TargetUserId != Guid.Empty)
            {
                tasks.Add(_hub.Clients
                    .User(message.TargetUserId.Value.ToString())
                    .SendAsync("EntityChanged", message));
            }

            await Task.WhenAll(tasks);

            _logger.LogDebug(
                "[RealtimeNotifier] Sent {Entity} {Action} → repo:{RepoKey}",
                message.Entity, message.Action, message.RepoKey ?? "none");
        }
    }
}