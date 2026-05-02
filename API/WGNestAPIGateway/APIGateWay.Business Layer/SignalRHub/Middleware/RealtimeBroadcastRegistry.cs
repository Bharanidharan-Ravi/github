using APIGateWay.ModalLayer.GETData;
using APIGateWay.ModalLayer.MasterData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.SignalRHub.Middleware
{
    public static class RealtimeBroadcastRegistry
    {
        // ── Ticket ─────────────────────────────────────────────────────────────
        public static readonly BroadcastEntityConfig<GetTickets> Ticket = new()
        {
            SyncConfigKey = "TicketsList",
            BuildSyncParams = dto => new Dictionary<string, string>
                                     { { "IssueId", dto.Issue_Id.ToString() } },
            MatchPredicate = (rich, base_) => rich.Issue_Id == base_.Issue_Id,
            Entity = RealtimeEntities.Ticket.Entity,
            KeyField = RealtimeEntities.Ticket.KeyField,
           GetRepoKey = dto => dto.RepoId.ToString(),
        };

        // ── Thread / Comments ──────────────────────────────────────────────────
        public static readonly BroadcastEntityConfig<ThreadList> Thread = new()
        {
            SyncConfigKey = "ThreadsList",
            BuildSyncParams = dto => new Dictionary<string, string>
                                     { { "IssueId", dto.Issue_Id.ToString() } },
            MatchPredicate = (rich, base_) => rich.ThreadId == base_.ThreadId,
            Entity = RealtimeEntities.ThreadsList.Entity,
            KeyField = RealtimeEntities.ThreadsList.KeyField,
            GetIssueId = dto => dto.Issue_Id,
        };

        // ── Ticket History ─────────────────────────────────────────────────────
        public static readonly BroadcastEntityConfig<TicketHistory> TicketHistory = new()
        {
            SyncConfigKey = "TicketHistoryList",
            BuildSyncParams = dto => new Dictionary<string, string>
                                     { { "IssueId", dto.IssueId.ToString() } },
            MatchPredicate = (rich, base_) => rich.Id == base_.Id,
            Entity = RealtimeEntities.TicketHistory.Entity,
            KeyField = RealtimeEntities.TicketHistory.KeyField,
            GetIssueId = dto => dto.IssueId,
        };

        // ── Project ────────────────────────────────────────────────────────────
        public static readonly BroadcastEntityConfig<GetProject> Project = new()
        {
            SyncConfigKey = "ProjectList",
            BuildSyncParams = dto => new Dictionary<string, string>
                                     { { "ProjectId", dto.Id.ToString() } },
            MatchPredicate = (rich, base_) => rich.Id == base_.Id,
            Entity = RealtimeEntities.Project.Entity,
            KeyField = RealtimeEntities.Project.KeyField,
            GetRepoKey = dto => dto.Repo_Id.ToString(),
        };

        // ── Employee ───────────────────────────────────────────────────────────
        public static readonly BroadcastEntityConfig<GetEmployee> Employee = new()
        {
            SyncConfigKey = "EmployeeList",
            BuildSyncParams = dto => new Dictionary<string, string>
                                     { { "EmployeeId", dto.UserID.ToString() } },
            MatchPredicate = (rich, base_) => rich.UserID == base_.UserID,
            Entity = RealtimeEntities.Employee.Entity,
            KeyField = RealtimeEntities.Employee.KeyField,
            //GetRepoKey = dto => dto.,
        };

        // ── Label ──────────────────────────────────────────────────────────────
        public static readonly BroadcastEntityConfig<GetLabel> Label = new()
        {
            SyncConfigKey = "LabelMaster",
            BuildSyncParams = dto => new Dictionary<string, string>
                                     { { "LabelId", dto.Id.ToString() } },
            MatchPredicate = (rich, base_) => rich.Id == base_.Id,
            Entity = RealtimeEntities.Label.Entity,
            KeyField = RealtimeEntities.Label.KeyField,
        };

        // ── RepoList ───────────────────────────────────────────────────────────
        public static readonly BroadcastEntityConfig<GetRepo> Repo = new()
        {
            SyncConfigKey = "RepoList",
            BuildSyncParams = dto => new Dictionary<string, string>
                                     { { "RepoId", dto.Repo_Id.ToString() } },
            MatchPredicate = (rich, base_) => rich.Repo_Id == base_.Repo_Id,
            Entity = RealtimeEntities.RepoList.Entity,
            KeyField = RealtimeEntities.RepoList.KeyField,
            GetRepoKey = dto => dto.Repo_Id.ToString(),
        };

        // ── Future entity template ─────────────────────────────────────────────
        // public static readonly BroadcastEntityConfig<GetDailyPlan> DailyPlan = new()
        // {
        //     SyncConfigKey   = "DailyPlanList",
        //     BuildSyncParams = dto => new Dictionary<string, string>
        //                              { { "PlanId", dto.Plan_Id.ToString() } },
        //     MatchPredicate  = (rich, base_) => rich.Plan_Id == base_.Plan_Id,
        //     Entity          = RealtimeEntities.DailyPlan.Entity,
        //     KeyField        = RealtimeEntities.DailyPlan.KeyField,
        //     GetRepoKey      = dto => dto.RepoKey,
        // };
    }
}
