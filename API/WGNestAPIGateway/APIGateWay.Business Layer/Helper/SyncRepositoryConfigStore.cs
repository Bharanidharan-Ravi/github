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
                EntityType = typeof(LabelMaster),
                SourceName = "SyncExecutionService",

                // Aggregation
                Type = "array",
                Strategy = "merge",
                IdKey = "Id",
                DeltaEnabled = true
            }
        };
    }


}
