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

        public TicketFeedbackRepo(
            APIGatewayDBContext db,
            IDomainService domainService,
            ILoginContextService loginContext,
            ITicketHistoryRepository historyRepository,
            IRealtimeNotifier realtimeNotifier,
            IRequestStepContext stepContext,
            APIGateWayCommonService commonService)
        {
            _db = db;
            _domainService = domainService;
            _loginContext = loginContext;
            _historyRepository = historyRepository;
            _realtimeNotifier = realtimeNotifier;
            _stepContext = stepContext;
            _commonService = commonService;
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

            await _domainService.ExecuteInTransactionAsync(async () =>
            {
                var entries = new List<TicketFeedback>();
                var historyEntries = new List<TicketHistoryEntry>();

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
                        CreatedAt = now
                    });

                    var targetUser = users.FirstOrDefault(u => u.EmployeeID == userId);
                    var targetName = targetUser?.EmployeeName ?? "Assigned Member";

                    historyEntries.Add(TicketHistoryHelper.FeedbackGiven(
                        dto.TicketId,
                        targetName,
                        dto.Rating,
                        dto.Comment,
                        actorId,
                        actorName
                        ));
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

            await _realtimeNotifier.BroadcastAsync(new RealtimeMessage
            {
                Entity = "TicketHistory",
                Action = "Create",
                Payload = new { IssueId = dto.TicketId },
                KeyField = "IssueId",
                RepoKey = $"repo-{dto.RepoId}",
                Timestamp = DateTime.UtcNow
            });

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
