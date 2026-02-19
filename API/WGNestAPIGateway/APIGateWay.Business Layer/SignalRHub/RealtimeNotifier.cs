using APIGateWay.ModalLayer.Hub;
using Microsoft.AspNetCore.SignalR;
using APIGateWay.BusinessLayer.SignalRHub;


namespace APIGateWay.BusinessLayer.SignalRHub
{
    public class RealtimeNotifier : IRealtimeNotifier
    {
        private readonly IHubContext<RealtimeHub> _hub;

        public RealtimeNotifier(IHubContext<RealtimeHub> hub)
        {
            _hub = hub;
        }

        public async Task BroadcastAsync(RealtimeMessage message)
        {
            //await _hub.Clients.All.SendAsync("EntityChanged", message);
            // Always notify admins
            await _hub.Clients.Group("global-admin")
                    .SendAsync("EntityChanged", message);

            // Notify repo users
            if (!string.IsNullOrWhiteSpace(message.RepoKey))
            {
                await _hub.Clients.Group($"repo-{message.RepoKey}")
                          .SendAsync("EntityChanged", message);
            }

            // Ensure creator always receives
            //if (message.CreatedBy != Guid.Empty)
            //{
            //    await _hub.Clients.User(message.CreatedBy.ToString())
            //        .SendAsync("EntityChanged", message);
            //}
            //if (message.RepoId.HasValue)
            //{
            //    await _hub.Clients.Groups(
            //            $"repo-{message.RepoId}",
            //            "global-admin"
            //        )
            //        .SendAsync("EntityChanged", message);
            //}
        }
    }

}