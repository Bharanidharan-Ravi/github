using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.SignalRHub.Middleware
{
    public class BroadcastEntityConfig<TDto> where TDto : class
    {
        /// <summary>Config key used in ISyncExecutionService.FetchRichDataAsync.</summary>
        public required string SyncConfigKey { get; init; }

        /// <summary>Builds the API params dict from the base DTO for FetchRichDataAsync.</summary>
        public required Func<TDto, Dictionary<string, string>> BuildSyncParams { get; init; }

        /// <summary>Finds the matching record in the sync result list.</summary>
        public required Func<TDto, TDto, bool> MatchPredicate { get; init; }

        /// <summary>Use RealtimeEntities.Xxx.Entity constant.</summary>
        public required string Entity { get; init; }

        /// <summary>Use RealtimeEntities.Xxx.KeyField constant.</summary>
        public required string KeyField { get; init; }

        /// <summary>Extracts the RepoKey for group routing.</summary>
        public Func<TDto, string?> GetRepoKey { get; init; }

        /// <summary>
        /// Extracts the IssueId for entities scoped to a ticket
        /// (ThreadsList, TicketHistory). Leave null for top-level entities.
        /// </summary>
        public Func<TDto, Guid?>? GetIssueId { get; init; }
    }
}
