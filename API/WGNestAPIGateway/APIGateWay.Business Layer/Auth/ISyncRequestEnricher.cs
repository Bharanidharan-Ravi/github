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

    public class EnrichedSyncRequest
    {
        /// <summary>
        /// Keys denied outright (e.g. RepoList for Role 3).
        /// Key = config key, Value = reason string.
        /// </summary>
        public Dictionary<string, string> DeniedKeys { get; } = new();

        /// <summary>
        /// One entry per (configKey, repoId) pair to execute.
        /// Role 1/2 → one entry per key with no repoId injection.
        /// Role 3    → one entry per (key × repo they belong to).
        /// </summary>
        public List<SyncExecutionUnit> Units { get; } = new();
    }

    /// <summary>
    /// A single unit of work: one config key with its resolved params.
    /// </summary>
    public class SyncExecutionUnit
    {
        public string ConfigKey { get; init; }
        public DateTimeOffset? LastSync { get; init; }
        public Dictionary<string, string> Params { get; init; } = new();
        /// <summary>
        /// Merge key for results when Role 3 has multiple repos.
        /// Usually same as ConfigKey. E.g. "TicketsList".
        /// </summary>
        public string ResultKey { get; init; }
    }
}