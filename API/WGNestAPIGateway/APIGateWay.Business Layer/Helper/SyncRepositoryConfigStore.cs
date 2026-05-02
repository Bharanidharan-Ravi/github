using APIGateWay.BusinessLayer.Configuration;
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
                StoredProcedure= "GETALLREPO",
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
            ["CheckedTickets"] = new SyncRepositoryConfig
            {
                // Execution
                SourceType = SyncSourceType.Local,
                StoredProcedure = "getdailyplan",
                EntityType = typeof(GetDailyPlan),
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
        };
    }


}
