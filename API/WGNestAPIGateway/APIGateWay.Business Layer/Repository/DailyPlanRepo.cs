using APIGateWay.Business_Layer.Interface;
using APIGateWay.DomainLayer.CommonSevice;
using APIGateWay.DomainLayer.DBContext;
using APIGateWay.DomainLayer.Helpers;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.GETData;
using APIGateWay.ModalLayer.MasterData;
using APIGateWay.ModalLayer.PostData;
using APIGateWay.ModelLayer.ErrorException;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Repository
{
    public class DailyPlanRepo : IDailyPlanRepo
    {
        private readonly IDomainService _domainService;
        private readonly ILoginContextService _loginContext;
        private readonly APIGatewayDBContext _db;
        private readonly APIGateWayCommonService _commonService;

        public DailyPlanRepo(
            IDomainService domainService,
            ILoginContextService loginContext,
            APIGatewayDBContext db,
            APIGateWayCommonService aPIGateWay)
        {
            _domainService = domainService;
            _loginContext = loginContext;
            _db = db;
            _commonService = aPIGateWay;
        }

        // ── GET today's plan ─────────────────────────────────────────────────
        public async Task<List<GetDailyPlan>> GetTodayPlanAsync(DateTime date)
        {
            var userId = _loginContext.userId;
            var dateOnly = date.Date;

            var plans = await _db.DailyPlans
         .Where(p => p.UserId == userId && p.PlannedDate.Date == dateOnly)
         .GroupJoin(
             _db.ISSUEMASTER,
             p => p.TicketId,
             t => t.Issue_Id,
             (p, tickets) => new { p, tickets })
         .SelectMany(
             x => x.tickets.DefaultIfEmpty(),   // LEFT JOIN — null ticket is fine
             (x, t) => new GetDailyPlan
             {
                 Id = x.p.Id,
                 TicketId = x.p.TicketId,
                 ProjKey = x.p.ProjKey,
                 PlannedDate = x.p.PlannedDate,
                 Status = x.p.Status,
                 StatusLabel = ToLabel(x.p.Status),
                 IsLocked = x.p.Status == DailyPlanStatus.Success,
                 UncheckComment = x.p.UncheckComment,
                 TicketTitle = t != null ? t.Title : "(ticket not found)",
                 IssueCode = t != null ? t.Issue_Code : "-",
                 CreatedAt = x.p.CreatedAt,
                 UpdatedAt = x.p.UpdatedAt,
             })
         .OrderByDescending(p => p.CreatedAt)
         .ToListAsync();

            return plans;
        }
        // ── CHECK ticket (add to plan) ────────────────────────────────────────
        public async Task<GetDailyPlan> CheckTicketAsync(CreateDailyPlanDto dto)
        {
            var userId = _loginContext.userId;
            var today = DateTime.UtcNow.Date;

            // Idempotent — return existing Active or Success row if found
            var existing = await _db.DailyPlans.FirstOrDefaultAsync(p =>
                p.UserId == userId &&
                p.TicketId == dto.TicketId &&
                p.PlannedDate.Date == today &&
                (p.Status == DailyPlanStatus.Active || p.Status == DailyPlanStatus.Success));

            if (existing != null)
                return await BuildResponse(existing);
            var seq = await _commonService.GetNextSequenceAsync("DailyPlanner");
            var plan = new DailyPlan
            {
                Id = seq.CurrentValue,
                UserId = userId,
                TicketId = dto.TicketId,
                ProjKey = dto.ProjKey,
                PlannedDate = today,
                Status = DailyPlanStatus.Active,
                CreatedAt = DateTime.UtcNow,
            };

            await _domainService.ExecuteInTransactionAsync(async () =>
            {
                await _domainService.SaveEntityWithAttachmentsAsync(plan, null);
                return true;
            });

            return await BuildResponse(plan);
        }

        // ── UNCHECK ticket ────────────────────────────────────────────────────
        public async Task<GetDailyPlan> UncheckTicketAsync(Guid planId, UncheckPlanDto dto)
        {
            var plan = await _db.DailyPlans.FindAsync(planId)
                ?? throw new Exceptionlist.DataNotFoundException($"Plan '{planId}' not found.");

            // Success is terminal — background job has confirmed thread exists
            if (plan.Status == DailyPlanStatus.Success)
                throw new InvalidOperationException(
                    "This ticket was completed successfully and cannot be unchecked.");

            // Already unchecked — idempotent
            if (plan.Status == DailyPlanStatus.Unchecked)
                return await BuildResponse(plan);

            await _domainService.ExecuteInTransactionAsync(async () =>
            {
                plan.Status = DailyPlanStatus.Unchecked;
                plan.UncheckComment = dto.UncheckComment;
                plan.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                return true;
            });

            return await BuildResponse(plan);
        }

        // ── Helpers ──────────────────────────────────────────────────────────
        private async Task<GetDailyPlan> BuildResponse(DailyPlan plan)
        {
            var ticket = await _db.ISSUEMASTER.FindAsync(plan.TicketId);
            return new GetDailyPlan
            {
                Id = plan.Id,
                TicketId = plan.TicketId,
                ProjKey = plan.ProjKey,
                PlannedDate = plan.PlannedDate,
                Status = plan.Status,
                StatusLabel = ToLabel(plan.Status),
                IsLocked = plan.Status == DailyPlanStatus.Success,
                UncheckComment = plan.UncheckComment,
                TicketTitle = ticket?.Title,
                IssueCode = ticket?.Issue_Code,
                CreatedAt = plan.CreatedAt,
                UpdatedAt = plan.UpdatedAt,
            };
        }

        private static string ToLabel(int status) => status switch
        {
            DailyPlanStatus.Active => "Active",
            DailyPlanStatus.Success => "Success",
            DailyPlanStatus.Failed => "Failed",
            DailyPlanStatus.Unchecked => "Unchecked",
            _ => "Unknown"
        };
    }
}
