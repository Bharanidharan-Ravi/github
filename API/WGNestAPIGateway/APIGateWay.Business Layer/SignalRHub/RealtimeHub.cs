// RealtimeHub.cs
using System.Security.Claims;
using APIGateWay.DomainLayer.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace APIGateWay.BusinessLayer.SignalRHub
{
    /// <summary>
    /// SignalR hub that manages group membership on connect/disconnect.
    ///
    /// Group scheme
    /// ─────────────
    ///  "global-admin"   → role 1 & 2 users  (receive all entity events)
    ///  "repo-{repoId}"  → role 3 users per repo they have access to
    ///
    /// Adding a new group type (e.g. project-level):
    ///  1. Add a GetUserProjectIdsAsync method to IRepoAccessService
    ///  2. Uncomment the project-group block below
    ///  3. Broadcast to "project-{id}" in RealtimeNotifier
    /// </summary>
    [Authorize]
    public class RealtimeHub : Hub
    {
        private readonly IRepoAccessService _repoAccess;
        private readonly ILogger<RealtimeHub> _logger;

        public RealtimeHub(
            IRepoAccessService repoAccess,
            ILogger<RealtimeHub> logger)
        {
            _repoAccess = repoAccess;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var role = Context.User?.FindFirst(ClaimTypes.Role)?.Value;
            var userIdString = Context.UserIdentifier;

            if (!Guid.TryParse(userIdString, out var userId))
            {
                _logger.LogWarning(
                    "[RealtimeHub] Rejected — invalid UserId: {Id}", userIdString);
                Context.Abort();
                return;
            }

            switch (role)
            {
                case "1":
                case "2":
                    await Groups.AddToGroupAsync(Context.ConnectionId, "global-admin");
                    _logger.LogInformation(
                        "[RealtimeHub] Admin connected. UserId={UserId}", userId);
                    break;

                case "3":
                    var repoIds = await _repoAccess.GetUserRepoIdsAsync(userId);
                    var joinTasks = repoIds.Select(id =>
                        Groups.AddToGroupAsync(Context.ConnectionId, $"repo-{id}"));
                    await Task.WhenAll(joinTasks);

                    _logger.LogInformation(
                        "[RealtimeHub] User connected. UserId={UserId} Repos={Count}",
                        userId, repoIds.Count());
                    break;

                default:
                    _logger.LogWarning(
                        "[RealtimeHub] Unknown role '{Role}' for UserId={UserId}. Aborting.",
                        role, userId);
                    Context.Abort();
                    return;
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (exception is not null)
                _logger.LogWarning(exception,
                    "[RealtimeHub] Disconnected with error. UserId={UserId}",
                    Context.UserIdentifier);
            else
                _logger.LogInformation(
                    "[RealtimeHub] Disconnected cleanly. UserId={UserId}",
                    Context.UserIdentifier);

            await base.OnDisconnectedAsync(exception);
        }
    }
}

//namespace APIGateWay.BusinessLayer.SignalRHub
//{
//    [Authorize]
//    public class RealtimeHub : Microsoft.AspNetCore.SignalR.Hub
//    {
//        private readonly IRepoAccessService _repoAccess;

//        public RealtimeHub(IRepoAccessService repoAccess)
//        {
//            _repoAccess = repoAccess;
//        }

//        public override async Task OnConnectedAsync()
//        {
//            var role = Context.User?.FindFirst(ClaimTypes.Role)?.Value;
//            var userIdString = Context.UserIdentifier;



//            if (!Guid.TryParse(userIdString, out var userId))
//            {
//                Context.Abort();
//                return;
//            }
//            Console.WriteLine($"User Connected: {userId}, Role: {role}");
//            if (role == "1" || role == "2")
//            {
//                await Groups.AddToGroupAsync(Context.ConnectionId, "global-admin");
//            }
//            else if (role == "3")
//            {
//                var repoIds = await _repoAccess.GetUserRepoIdsAsync(userId);

//                foreach (var repoId in repoIds)
//                {
//                    await Groups.AddToGroupAsync(
//                        Context.ConnectionId,
//                        $"repo-{repoId}"
//                    );
//                }
//            }

//            await base.OnConnectedAsync();
//        }

//        public override async Task OnDisconnectedAsync(Exception? exception)
//        {
//            // Optional: handle presence tracking here
//            await base.OnDisconnectedAsync(exception);
//        }

//    }

//}
