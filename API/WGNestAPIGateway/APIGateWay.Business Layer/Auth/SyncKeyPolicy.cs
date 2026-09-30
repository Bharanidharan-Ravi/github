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

        /// <summary>
        /// When set (and IsRepoScoped): instead of fanning out, ONE unit is
        /// executed with every allowed repo id as a CSV in this param. Needed
        /// by SPs that page or count — per-repo calls would each return their
        /// own page / counts.
        /// </summary>
        public string? RepoListParamKey { get; init; }
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

                // Same scoping as TicketsList — without this entry Role 3
                // would pass through unscoped and see every repo. Paged, so all
                // repos go in one call (@RepoIds) instead of a fan-out.
                ["TicketListV2"] = new SyncKeyRule
                {
                    AllowedRoles = AppRoles.All,
                    IsRepoScoped = true,
                    RepoListParamKey = "repoIds"
                },

                // Filter / tab counts for TicketListV2 — same scope.
                ["TicketListCountsV2"] = new SyncKeyRule
                {
                    AllowedRoles = AppRoles.All,
                    IsRepoScoped = true,
                    RepoListParamKey = "repoIds"
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
                    AllowedRoles = AppRoles.AdminManager,
                    IsRepoScoped = false
                },

                //["LabelMaster"] = new SyncKeyRule
                //{
                //    AllowedRoles = AppRoles.All,
                //    IsRepoScoped = false
                //},
            };
    }
}