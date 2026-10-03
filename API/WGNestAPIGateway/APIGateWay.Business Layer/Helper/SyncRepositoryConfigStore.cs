using APIGateWay.BusinessLayer.Configuration;
using APIGateWay.ModalLayer.DTOs;
using APIGateWay.ModalLayer.GETData;
using APIGateWay.ModalLayer.MasterData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.BusinessLayer.Helper
{
    public static class SyncRepositoryConfigStore
    {
        public static readonly Dictionary<string, SyncRepositoryConfig> Configs = new()
        {
            ["ProjectList"] = new SyncRepositoryConfig
            {
                // Execution
                SourceType = SyncSourceType.Local,
                StoredProcedure = "GetAllProjData",
                EntityType = typeof(GetProject),
                SourceName = "ProjectService",

                // Aggregation
                Type = "array",
                Strategy = "merge",
                IdKey = "projectId",
                DeltaEnabled = true
            },

            ["RepoList"] = new SyncRepositoryConfig
            {
                // Execution
                SourceType = SyncSourceType.Local,
                StoredProcedure = "GETALLREPO",
                EntityType = typeof(GetRepo),
                SourceName = "SyncExecutionService",

                // Aggregation
                Type = "array",
                Strategy = "merge",
                IdKey = "repoId",
                DeltaEnabled = true
            },

            ["TicketsList"] = new SyncRepositoryConfig
            {
                // Execution
                SourceType = SyncSourceType.Local,
                StoredProcedure = "GetIssuesByID",
                EntityType = typeof(GetTickets),
                SourceName = "SyncExecutionService",

                // Aggregation
                Type = "array",
                Strategy = "merge",
                IdKey = "repoId",
                DeltaEnabled = true

            },

            // Slim ticket list (ids only). UI sends one "Filters" JSON param;
            // new filters/sorts are rows in dbo.TicketListQueryDef, no API change.
            // Detail / edit keep using "TicketsList".
            ["TicketListV2"] = new SyncRepositoryConfig
            {
                // Execution
                SourceType = SyncSourceType.Local,
                StoredProcedure = "GetTicketList_V2",
                EntityType = typeof(GetTicketListRow),
                SourceName = "SyncExecutionService",

                // Aggregation
                Type = "array",
                Strategy = "merge",
                IdKey = "Issue_Id",
                DeltaEnabled = true,

                // Resolved from the JWT, never trusted from the frontend.
                IdentityParams = new Dictionary<string, IdentityField>
                {
                    ["Role"] = IdentityField.Role,
                    ["UserId"] = IdentityField.UserId
                }
            },

            // Total + per-value counts for the TicketListV2 dropdowns / status
            // tabs (same "Filters" JSON). New facets are rows in
            // dbo.TicketListQueryDef (Kind 'C'), no API change.
            ["TicketListCountsV2"] = new SyncRepositoryConfig
            {
                // Execution
                SourceType = SyncSourceType.Local,
                StoredProcedure = "GetTicketListCounts_V2",
                EntityType = typeof(GetTicketListCount),
                SourceName = "SyncExecutionService",

                // Aggregation
                Type = "array",
                Strategy = "merge",
                IdKey = "Id",
                DeltaEnabled = false,

                // Resolved from the JWT, never trusted from the frontend.
                IdentityParams = new Dictionary<string, IdentityField>
                {
                    ["Role"] = IdentityField.Role,
                    ["UserId"] = IdentityField.UserId
                }
            },

            ["EmployeeList"] = new SyncRepositoryConfig
            {
                // Execution
                SourceType = SyncSourceType.Local,
                StoredProcedure = "GetEmployeeMaster",
                EntityType = typeof(GetEmployee),
                SourceName = "SyncExecutionService",

                // Aggregation
                Type = "array",
                Strategy = "merge",
                IdKey = "UserID",
                DeltaEnabled = true
            },

            ["LabelMaster"] = new SyncRepositoryConfig
            {
                // Execution
                SourceType = SyncSourceType.Local,
                StoredProcedure = "GETLABELMASTER",
                EntityType = typeof(GetLabel),
                SourceName = "SyncExecutionService",

                // Aggregation
                Type = "array",
                Strategy = "merge",
                IdKey = "Id",
                DeltaEnabled = true
            },

            ["TimeSheet"] = new SyncRepositoryConfig
            {
                // Execution
                SourceType = SyncSourceType.Local,
                StoredProcedure = "DashBoardTimesheetData",
                EntityType = typeof(DashBoardTimeSheetData),
                SourceName = "SyncExecutionService",

                // Aggregation
                Type = "array",
                Strategy = "merge",
                IdKey = "Id",
                DeltaEnabled = true
            },
            ["ThreadsList"] = new SyncRepositoryConfig
            {
                // Execution
                SourceType = SyncSourceType.Local,
                StoredProcedure = "GETTHREADLIST",
                EntityType = typeof(ThreadList),
                SourceName = "SyncExecutionService",

                // Aggregation
                Type = "array",
                Strategy = "merge",
                IdKey = "IssuesId",
                DeltaEnabled = true
            },
            ["TicketIssueLog"] = new SyncRepositoryConfig
            {
                // Execution
                SourceType = SyncSourceType.Local,
                StoredProcedure = "GETTICKETISSUELOG",
                EntityType = typeof(GetIssueLogList),
                SourceName = "SyncExecutionService",
                // Aggregation
                Type = "array",
                Strategy = "merge",
                IdKey = "IssuesId",
                DeltaEnabled = true
            },
            ["StatusMaster"] = new SyncRepositoryConfig
            {
                // Execution
                SourceType = SyncSourceType.Local,
                StoredProcedure = "GetStatusMaster",
                EntityType = typeof(StatusMaster),
                SourceName = "SyncExecutionService",

                // Aggregation
                Type = "array",
                Strategy = "merge",
                IdKey = "IssuesId",
                DeltaEnabled = true
            },
            // Daily plan rows only (plan id, TicketId, user); the UI loads the
            // tickets from "TicketListV2" with the "issue" filter.
            ["CheckedTickets"] = new SyncRepositoryConfig
            {
                // Execution
                SourceType = SyncSourceType.Local,
                StoredProcedure = "GetDailyPlan_V2",
                EntityType = typeof(GetDailyPlanRow),
                SourceName = "SyncExecutionService",

                // Aggregation
                Type = "array",
                Strategy = "merge",
                IdKey = "IssuesId",
                DeltaEnabled = true
            },
            ["TicketHistory"] = new SyncRepositoryConfig
            {
                // Execution
                SourceType = SyncSourceType.Local,
                StoredProcedure = "GetTicketHistory",
                EntityType = typeof(TicketHistory),
                SourceName = "SyncExecutionService",

                // Aggregation
                Type = "array",
                Strategy = "merge",
                IdKey = "IssuesId",
                DeltaEnabled = true
            },
            ["TeamMaster"] = new SyncRepositoryConfig
            {
                // Execution
                SourceType = SyncSourceType.Local,
                StoredProcedure = "GetTeamMaster",
                EntityType = typeof(TeamMaster),
                SourceName = "SyncExecutionService",

                // Aggregation
                Type = "array",
                Strategy = "merge",
                IdKey = "IssuesId",
                DeltaEnabled = true
            },
            ["TicketProgress"] = new SyncRepositoryConfig
            {
                // Execution
                SourceType = SyncSourceType.Local,
                StoredProcedure = "GetTicketProgressLogsByIssueId",
                EntityType = typeof(TicketProgressLogDto),
                SourceName = "SyncExecutionService",

                // Aggregation
                Type = "array",
                Strategy = "merge",
                IdKey = "IssuesId",
                DeltaEnabled = true
            },
            ["ClientData"] = new SyncRepositoryConfig
            {
                SourceType = SyncSourceType.Local,
                StoredProcedure = "GetRepoUser",
                EntityType = typeof(GetRepoUserData),
                SourceName = "SyncExecutionService",
                Type = "array",
                Strategy = "merge",
                IdKey = "repoId",
                DeltaEnabled = true
            }
            ,
            ["MeetingData"] = new SyncRepositoryConfig
            {
                SourceType = SyncSourceType.Local,
                StoredProcedure = "sp_GetMeetings",
                EntityType = typeof(GetMeetingDto),
                SourceName = "SyncExecutionService",
                Type = "array",
                Strategy = "merge",
                IdKey = "repoId",
                DeltaEnabled = true
            },
            ["UpcomingMeeting"] = new SyncRepositoryConfig
            {
                SourceType = SyncSourceType.Local,
                StoredProcedure = "Sp_GetUpcomingMeetings",
                EntityType = typeof(GetUpcomingMeeting),
                SourceName = "SyncExecutionService",
                Type = "array",
                Strategy = "merge",
                IdKey = "repoId",
                DeltaEnabled = true
            },

            ["BannerData"]= new SyncRepositoryConfig
            {
                SourceType = SyncSourceType.Local,
                StoredProcedure = "GetBannerMessage",
                EntityType = typeof(GetBannerMessageSP),
                SourceName = "SyncExecutionService",
                Type = "array",
                Strategy = "merge",
                IdKey = "repoId",
                DeltaEnabled = true
            },
            ["BannerDataType"] = new SyncRepositoryConfig
            {
                SourceType = SyncSourceType.Local,
                StoredProcedure = "GetAllBannerMessageType",
                EntityType = typeof(BannerMessageType),
                SourceName = "SyncExecutionService",
                Type = "array",
                Strategy = "merge",
                IdKey = "repoId",
                DeltaEnabled = true
            },
            ["Emoji_Reactions"] = new SyncRepositoryConfig
            {
                SourceType = SyncSourceType.Local,
                StoredProcedure = "GetEmoji",
                EntityType = typeof(Emoji_Reactions),
                SourceName = "SyncExecutionService",
                Type = "array",
                Strategy = "merge",
                IdKey = "repoId",
                DeltaEnabled = true
            },
           
            ["GetStaleTicketsForAssignee"] = new SyncRepositoryConfig
            {
                SourceType = SyncSourceType.Local,
                StoredProcedure = "GetStaleTicketsForAssignee",
                EntityType = typeof(GetStaleTicketsForAssignee),
                SourceName = "SyncExecutionService",
                Type = "array",
                Strategy = "merge",
                IdKey = "repoId",
                DeltaEnabled = true,

                // Always the caller's own stale tickets (GET /notification/counts sends no params)
                IdentityParams = new Dictionary<string, IdentityField>
                {
                    ["Assignee_Id"] = IdentityField.UserId
                }
            },
            ["GetUserOnlineStatus"] = new SyncRepositoryConfig
            {
                SourceType = SyncSourceType.Local,
                StoredProcedure = "GetAllUserOnlineStatus",
                EntityType = typeof(GetAllUserOnlineStatus),
                SourceName = "SyncExecutionService",
                Type = "array",
                Strategy = "merge",
                IdKey = "repoId",
                DeltaEnabled = true
            },
            ["TicketFeedback"] = new SyncRepositoryConfig
            {
                SourceType = SyncSourceType.Local,
                StoredProcedure = "GetTicketFeedbacks",
                EntityType = typeof(GetTicketFeedbackDto),
                SourceName = "SyncExecutionService",
                Type = "array",
                Strategy = "merge",
                IdKey = "FeedbackId",
                DeltaEnabled = false
            },
            ["LEAVETYPE"] = new SyncRepositoryConfig
            {
                // Execution
                SourceType = SyncSourceType.Local,
                StoredProcedure = "WG_LEAVETYPE",
                EntityType = typeof(LeaveType),
                SourceName = "SyncExecutionService",

                // Aggregation
                Type = "array",
                Strategy = "merge",
                IdKey = "code",
                DeltaEnabled = true
            },
            ["GetLeaveRequests"] = new SyncRepositoryConfig
            {
                // Execution
                SourceType = SyncSourceType.Local,
                StoredProcedure = "usp_GetLeaveRequests",
                EntityType = typeof(GetLeaveRequest),
                SourceName = "SyncExecutionService",

                // Aggregation
                Type = "array",
                Strategy = "merge",
                IdKey = "ID",
                DeltaEnabled = false,

                // usp_GetLeaveRequests(@UserId, @IsAdmin) — resolved from the JWT,
                // never sent by the frontend.
                IdentityParams = new Dictionary<string, IdentityField>
                {
                    ["UserId"] = IdentityField.UserId,
                    ["IsAdmin"] = IdentityField.IsAdmin
                }
            },
            ["AllHour"] = new SyncRepositoryConfig
            {
                SourceType = SyncSourceType.Local,
                StoredProcedure = "GetIssueLoggedHours",
                EntityType = typeof(AllHour),
                SourceName = "SyncExecutionService",
                Type = "array",
                Strategy = "merge",
                IdKey = "Issue_Id",
                DeltaEnabled = false
            },
            ["GetPermissionRequests"] = new SyncRepositoryConfig
            {
                // Execution
                SourceType = SyncSourceType.Local,
                StoredProcedure = "usp_GetPermissionRequests",
                EntityType = typeof(GetPermissionRequest),
                SourceName = "SyncExecutionService",

                // Aggregation
                Type = "array",
                Strategy = "merge",
                IdKey = "ID",
                DeltaEnabled = false,

                // usp_GetPermissionRequests(@UserId, @IsAdmin) — resolved from the JWT,
                // never sent by the frontend.
                IdentityParams = new Dictionary<string, IdentityField>
                {
                    ["UserId"] = IdentityField.UserId,
                    ["IsAdmin"] = IdentityField.IsAdmin
                }
            },
        };
    }
}
