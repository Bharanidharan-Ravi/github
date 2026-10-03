using APIGateWay.Business_Layer.Interface;
using APIGateWay.Business_Layer.Session;
using APIGateWay.BusinessLayer.Helpers;
using APIGateWay.BusinessLayer.Interface;
using APIGateWay.BusinessLayer.Repository;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.DTOs;
using APIGateWay.ModalLayer.MasterData;
using Azure;
using Microsoft.AspNetCore.Mvc;

namespace APIGateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationController :ControllerBase
    {
        private readonly INotificationRepository _notificationRepository;
        private readonly ILoginContextService _loginContext;
        private readonly ILeaveRequestRepo _leaveRequestRepo;
        private readonly ISyncRepositoryV2 _syncRepo;
        public NotificationController(INotificationRepository notificationRepository, ILoginContextService loginContext,
            ILeaveRequestRepo leaveRequestRepo, ISyncRepositoryV2 syncRepo)
        {
            _notificationRepository = notificationRepository;
            _loginContext = loginContext;
            _leaveRequestRepo = leaveRequestRepo;
            _syncRepo = syncRepo;
        }

        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var count =
                await _notificationRepository
                    .GetUnreadCountAsync(
                        _loginContext.userId
                        );
            /*
                        return Ok(
                            new NotificationCountResponse
                            {
                                UnreadCount = count
                            });*/
           return Ok(ApiResponseHelper.Success(count));
        }

        /// <summary>
        /// GET /notification/counts — every header/sidebar badge in one call, in the
        /// /sync/v2 Res shape (each key ok/fails on its own):
        ///   UnreadCount                → unseen notifications per type { TICKET, MEETING, LEAVE_REQUEST }
        ///   LeaveRequestCount          → admin: requests awaiting a decision (drops only when decided);
        ///                                others: unseen decisions on their own requests (drops when
        ///                                the Leave Requests page marks LEAVE_REQUEST seen)
        ///   GetStaleTicketsForAssignee → the caller's stale tickets
        /// The client loads it once and refreshes it on realtime events / mark-seen — no polling.
        /// </summary>
        [HttpGet("counts")]
        public async Task<IActionResult> GetCounts()
        {
            // Sync keys run in their own DB scope, so this overlaps the EF queries below
            var syncTask = _syncRepo.RunAsync("GetStaleTicketsForAssignee");

            Dictionary<string, int> unread = null;
            var unreadResult = await SyncResults.FromAsync(async () =>
                unread = await _notificationRepository.GetUnreadCountAsync(_loginContext.userId));

            var leaveResult = _loginContext.role == 1
                ? await SyncResults.FromAsync(_leaveRequestRepo.CountPendingAsync)
                : unreadResult.Ok
                    ? SyncResults.Success(unread.GetValueOrDefault("LEAVE_REQUEST"))
                    : unreadResult;

            var response = await syncTask;
            response.Res["UnreadCount"] = unreadResult;
            response.Res["LeaveRequestCount"] = leaveResult;

            return this.SyncResult(response);
        }

        [HttpGet("list")]
        public async Task<IActionResult> GetNotificationsAsync()
        {
            var count =
                await _notificationRepository.GetNotificationsAsync(_loginContext.userId);
           return Ok( count );
        }
        [HttpPost("mark-seen")]
        public async Task<IActionResult> MarkSeen([FromBody] MarkSeenBody request)
        {
            await _notificationRepository
                .MarkSeenAsync(
                    _loginContext.userId, request.NotificationType);

            return Ok();
        }
    }
}
