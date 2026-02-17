using APIGateWay.Business_Layer.Configuration;
using APIGateWay.ModalLayer.GETData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Helper
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
            }
        };
    }


}
