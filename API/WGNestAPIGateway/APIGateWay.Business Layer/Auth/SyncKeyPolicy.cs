// ─────────────────────────────────────────────────────────────────────────────
// Namespace : APIGateWay.Business_Layer.Auth
// File      : SyncKeyPolicy.cs
// Purpose   : Per-key RBAC rules consumed by SyncRequestEnricher.
//             Adding a new sync key = adding ONE entry to Rules below.
// ─────────────────────────────────────────────────────────────────────────────
using APIGateWay.ModalLayer;

namespace APIGateWay.BusinessLayer.Auth
{
    public sealed class SyncKeyRule
    {
        /// <summary>Roles allowed to call this config key at all.</summary>
        public int[] AllowedRoles { get; init; } = AppRoles.All;

        /// <summary>
        /// When true: enricher fans out one execution unit per allowed repo,
        /// auto-injecting repoId into params. Frontend sends nothing extra.
        /// Applies to Role 2 and 3 — Role 1 always gets everything.
        /// </summary>
        public bool IsRepoScoped { get; init; }

        /// <summary>SP param name for the repo filter. Defaults to "repoId".</summary>
        public string RepoParamKey { get; init; } = "repoId";
    }

    public static class SyncKeyPolicy
    {
        public static readonly IReadOnlyDictionary<string, SyncKeyRule> Rules =
            new Dictionary<string, SyncKeyRule>(StringComparer.Ordinal)
            {
                // Role 3 completely blocked; Role 2 gets their own repos
                ["RepoList"] = new SyncKeyRule
                {
                    AllowedRoles = AppRoles.All,
                    IsRepoScoped = true,
                    RepoParamKey = "repoId"
                },

                // All roles — Role 2 + 3 get scoped automatically
                ["TicketsList"] = new SyncKeyRule
                {
                    AllowedRoles = AppRoles.All,
                    IsRepoScoped = true,
                    RepoParamKey = "repoId"
                },

                ["ProjectList"] = new SyncKeyRule
                {
                    AllowedRoles = AppRoles.All,
                    IsRepoScoped = true,
                    RepoParamKey = "repoId"
                },

                // Not scoped — all roles get the global list
                ["EmployeeList"] = new SyncKeyRule
                {
                    AllowedRoles = AppRoles.All,
                    IsRepoScoped = false
                },

                ["LabelMaster"] = new SyncKeyRule
                {
                    AllowedRoles = AppRoles.All,
                    IsRepoScoped = false
                },
            };
    }
}