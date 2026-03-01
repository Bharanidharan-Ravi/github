// APIGateWay.BusinessLayer.Auth.ISyncRequestEnricher
//
// For Role 3:
//   - Repo-scoped keys (TicketsList, ProjectList) → auto-inject their repoIds
//   - Blocked keys (RepoList) → mark as denied
//
// For Roles 1 and 2: request passes through unchanged.

using APIGateWay.ModalLayer.nugetmodal;

namespace APIGateWay.BusinessLayer.Auth
{
    public interface ISyncRequestEnricher
    {
        Task<EnrichedSyncRequest> EnrichAsync(DynamicSyncRequest request);
    }

    /// <summary>Result of enrichment — what to deny immediately and what to execute.</summary>
    public sealed class EnrichedSyncRequest
    {
        /// <summary>Keys blocked outright. Key = config key, Value = reason. No DB call made.</summary>
        public Dictionary<string, string> DeniedKeys { get; } = new();

        /// <summary>
        /// Resolved execution units.
        /// Role 1       → 1 unit per key, no injection.
        /// Role 2 / 3   → 1 unit per (key × allowed repo) for scoped keys.
        /// </summary>
        public List<SyncExecutionUnit> Units { get; } = new();
    }

    /// <summary>One resolved unit of work for SyncRepositoryV2.ExecuteUnitsAsync.</summary>
    public sealed class SyncExecutionUnit
    {
        public string ConfigKey { get; init; } = string.Empty;
        public string ResultKey { get; init; } = string.Empty;
        public DateTimeOffset? LastSync { get; init; }
        public Dictionary<string, string> Params { get; init; } = new();
    }
}