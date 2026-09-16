using APIGateWay.Business_Layer.Interface;
using APIGateWay.DomainLayer.CommonSevice;
using APIGateWay.DomainLayer.DBContext;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.MasterData;
using APIGateWay.ModalLayer.PostData;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Repository
{
    public class CurrentHolderRepo : ICurrentHolderRepo
    {
        private readonly APIGatewayDBContext _db;
        private readonly IDomainService _domainService;
        private readonly ILoginContextService _loginContext;         // ← ADDED

        public CurrentHolderRepo(
            APIGatewayDBContext db,
            IDomainService domainService,
            ILoginContextService loginContext)                        // ← ADDED
        {
            _db = db;
            _domainService = domainService;
            _loginContext = loginContext;

        }
        public async Task<List<Assignee_To_Move>> UpdateCurrentHolderAsync(Guid issueId, postAssigneeToMoveDto dto)
        {
            var ticket = await _db.Set<TicketMaster>()
                .Where(t => t.Issue_Id == issueId)
                .Select(t => new { t.RaiseToClient })
                .FirstOrDefaultAsync();

            if (ticket == null)
                throw new Exception("Ticket not found.");
            if (ticket.RaiseToClient && (dto.Assignee_To_Move == null || !dto.Assignee_To_Move.Any()))
                throw new Exception("Assignee is required for client tickets and cannot be cleared.");
            var posterId = _loginContext.userId;
            var indiaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            var indiaTime=TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,indiaTimeZone);
            return await _domainService.ExecuteInTransactionAsync(async () =>
            {
                var oldMoveTo = await _db.Set<IssueMoveTo>()
                .Where(x => x.Issue_Id == issueId)
                .ToListAsync();
                if (oldMoveTo.Any())
                    _db.Set<IssueMoveTo>().RemoveRange(oldMoveTo);
                var incoming = dto.Assignee_To_Move ?? new List<Assignee_To_Move>();
                foreach (var item in incoming)
                {
                    var issueMoveTo = new IssueMoveTo
                    {
                        Id = Guid.NewGuid(),
                        Issue_Id = issueId,
                        Move_to = item.id,
                        CreatedBy = posterId,
                        CreatedAt = DateTime.UtcNow,
                    };
                    await _db.Set<IssueMoveTo>().AddAsync(issueMoveTo);
                }
                await _db.SaveChangesAsync();
                return incoming;
            });
        }
    }
}
