using APIGateWay.Business_Layer.Interface;
using APIGateWay.BusinessLayer.SignalRHub;
using APIGateWay.DomainLayer.CommonSevice;
using APIGateWay.DomainLayer.DBContext;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.DTOs;
using APIGateWay.ModalLayer.GETData;
using APIGateWay.ModalLayer.Hub;
using APIGateWay.ModalLayer.MasterData;
using APIGateWay.ModalLayer.PostData;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using APIGateWay.Business_Layer.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using APIGateWay.Business_Layer.Session;

namespace APIGateWay.Business_Layer.Repository
{
    public class TicketFeedbackRepo : ITicketFeedbackRepo
    {
        private readonly APIGatewayDBContext _db;
        private readonly IDomainService _domainService;
        private readonly ILoginContextService _loginContext;
        private readonly ITicketHistoryRepository _historyRepository;
        private readonly IRealtimeNotifier _realtimeNotifier;
        private readonly IRequestStepContext _stepContext;
        private readonly APIGateWayCommonService _commonService;
        private readonly INotificationRepository _notificationRepository;

        public TicketFeedbackRepo(
            APIGatewayDBContext db,
            IDomainService domainService,
            ILoginContextService loginContext,
            ITicketHistoryRepository historyRepository,
            IRealtimeNotifier realtimeNotifier,
            IRequestStepContext stepContext,
            APIGateWayCommonService commonService,
            INotificationRepository notificationRepository)
        {
            _db = db;
            _domainService = domainService;
            _loginContext = loginContext;
            _historyRepository = historyRepository;
            _realtimeNotifier = realtimeNotifier;
            _stepContext = stepContext;
            _commonService = commonService;
            _notificationRepository = notificationRepository;   
        }

        public async Task<bool> SubmitFeedbackAsync(PostTicketFeedbackDto dto)
        {
            var actorId = _loginContext.userId;
            var actorName = _loginContext.userName;
            var now = DateTime.UtcNow;

            var users = await _db.eMPLOYEEMASTERs
                .Where(e => dto.UserIds.Contains(e.EmployeeID))
                .Select(e => new { e.EmployeeID, e.EmployeeName })
                .ToListAsync();

            var userNotifications = new List<(Guid UserId, string Message)>();
            var isThread = dto.FeedbackType == "Thread";

            await _domainService.ExecuteInTransactionAsync(async () =>
            {
                var entries = new List<TicketFeedback>();
                var historyEntries = new List<TicketHistoryEntry>();
                var feedbackType = string.IsNullOrEmpty(dto.FeedbackType) ? "Ticket" : dto.FeedbackType;

                foreach (var userId in dto.UserIds)
                {
                    entries.Add(new TicketFeedback
                    {
                        FeedbackId = Guid.NewGuid(),
                        RepoId = dto.RepoId,
                        TicketId = dto.TicketId,
                        UserId = userId,
                        Rating = dto.Rating,
                        Comment = dto.Comment,
                        CreatedBy = actorId,
                        CreatedAt = now,
                        ThreadId = dto.ThreadId,
                        FeedbackType = feedbackType
                    });

                    var targetUser = users.FirstOrDefault(u => u.EmployeeID == userId);
                    var targetName = targetUser?.EmployeeName ?? "Assigned Member";

                   var historyEntry = TicketHistoryHelper.FeedbackGiven(
                        dto.TicketId,
                        targetName,
                        dto.Rating,
                        dto.Comment,
                        actorId,
                        actorName
                        );
                    historyEntries.Add(historyEntry);
                    userNotifications.Add((userId, historyEntry.Summary));
                }

                var timer = _stepContext.StartStep();
                try
                {
                    await _db.TicketFeedbacks.AddRangeAsync(entries);
                    await _db.SaveChangesAsync();
                    _stepContext.Success("TicketFeedback", "INSERT", dto.TicketId.ToString(), timer);
                }
                catch (Exception ex)
                {
                    _stepContext.Failure("TicketFeedback", "INSERT", ex.Message, ex.InnerException?.Message, timer);
                    throw;
                }

                await _historyRepository.LogManyAsync(historyEntries);
                return true;
            });

            if (isThread && dto.ThreadId.HasValue)
            {
                await _realtimeNotifier.BroadcastAsync(new RealtimeMessage
                {
                    Entity = "ThreadList",
                    Action = "Update",
                    Payload = new { IssueId = dto.TicketId, ThreadId = dto.ThreadId.Value },
                    KeyField = "ThreadId",
                    RepoKey = $"repo-{dto.RepoId}",
                    Timestamp = DateTime.UtcNow

                });
            }
            else
            {
                await _realtimeNotifier.BroadcastAsync(new RealtimeMessage
                {
                    Entity = "TicketHistory",
                    Action = "Create",
                    Payload = new { IssueId = dto.TicketId },
                    KeyField = "IssueId",
                    RepoKey = $"repo-{dto.RepoId}",
                    Timestamp = DateTime.UtcNow
                });
            }

            try
            {
                 isThread = string.Equals(dto.FeedbackType, "Thread", StringComparison.OrdinalIgnoreCase);
                foreach (var notification in userNotifications)
                {
                    var notificationId = await _notificationRepository.CreateAsync(new CreateNotificationRequest
                    {
                        EventType = isThread ? "THREAD_FEEDBACK" : "TICKET_FEEDBACK",
                        EntityType = "TICKET",
                        EntityId = dto.TicketId.ToString(),
                        Title = isThread ? "New Thread Feedback" : "New Ticket Feedback",
                        Message = notification.Message,
                        ActorId = actorId,
                        ActorName = actorName,
                        Audiences = new List<NotificationAudience>
                        {
                            new NotificationAudience { AudienceType = "USER", AudienceValue = notification.UserId.ToString() }
                        }
                    });

                    await _realtimeNotifier.BroadcastAsync(new RealtimeMessage
                    {
                        Entity = "Notification",
                        Action = "Created",
                        Payload = new { NotificationId = notificationId, CreatedByUserId = actorId },
                        KeyField = "NotificationId",
                        TargetUserId = notification.UserId,
                        Timestamp = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("error");
            }

            return true;
        }

        public async Task<List<GetTicketFeedbackDto>> GetFeedbacksByTicketAsync(Guid ticketId)
        {
            var parameters = new[]
            {
                new SqlParameter("@DbName", _loginContext.databaseName ?? string.Empty),
                new SqlParameter("@TicketId", ticketId)
            };

            var list = await _commonService.ExecuteGetItemAsyc<GetTicketFeedbackDto>(
                "GetTicketFeedbacks",
                parameters
                );
            return list ?? new List<GetTicketFeedbackDto>();
        }
        
    }
}
