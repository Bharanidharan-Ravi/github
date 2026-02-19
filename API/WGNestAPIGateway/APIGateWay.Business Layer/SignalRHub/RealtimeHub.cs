using System.Security.Claims;
using APIGateWay.DomainLayer.Interface;
using Microsoft.AspNetCore.Authorization;

namespace APIGateWay.BusinessLayer.SignalRHub
{
    [Authorize]
    public class RealtimeHub : Microsoft.AspNetCore.SignalR.Hub
    {
        private readonly IRepoAccessService _repoAccess;

        public RealtimeHub(IRepoAccessService repoAccess)
        {
            _repoAccess = repoAccess;
        }

        public override async Task OnConnectedAsync()
        {
            var role = Context.User?.FindFirst(ClaimTypes.Role)?.Value;
            var userIdString = Context.UserIdentifier;

           

            if (!Guid.TryParse(userIdString, out var userId))
            {
                Context.Abort();
                return;
            }
            Console.WriteLine($"User Connected: {userId}, Role: {role}");
            if (role == "1" || role == "2")
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "global-admin");
            }
            else if (role == "3")
            {
                var repoIds = await _repoAccess.GetUserRepoIdsAsync(userId);

                foreach (var repoId in repoIds)
                {
                    await Groups.AddToGroupAsync(
                        Context.ConnectionId,
                        $"repo-{repoId}"
                    );
                }
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // Optional: handle presence tracking here
            await base.OnDisconnectedAsync(exception);
        }

    }

}
