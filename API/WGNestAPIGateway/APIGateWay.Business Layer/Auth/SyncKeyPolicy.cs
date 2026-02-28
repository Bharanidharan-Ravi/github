using APIGateWay.ModalLayer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Auth
{
    public class SyncKeyRule
    {
        /// <summary>Roles allowed to call this config key at all.</summary>
        public int[] AllowedRoles { get; init; } = AppRoles.All;

        /// <summary>
        /// When true and the caller is Role 3, the repoId param is validated
        /// against the user's RepoUsers access list.
        /// </summary>
        public bool IsRepoScoped { get; init; } = false;

        /// <summary>
        /// The param dictionary key that holds the repoId for this config key.
        /// Defaults to "repoId" — change per config if your SP uses a different name.
        /// </summary>
        public string RepoParamKey { get; init; } = "repoId";
    }

    public static class SyncKeyPolicy
    {
        public static readonly Dictionary<string, SyncKeyRule> Rules = new()
        {
            // Role 3 cannot see the full repo list — they only see their own repos
            ["RepoList"] = new SyncKeyRule
            {
                AllowedRoles = AppRoles.AdminManager,   // ← Role 3 blocked
                IsRepoScoped = false
            },

            // Role 3 can get tickets BUT only for repos they belong to
            ["TicketsList"] = new SyncKeyRule
            {
                AllowedRoles = AppRoles.All,
                IsRepoScoped = true,                    // ← repoId validated for Role 3
                RepoParamKey = "repoId"
            },

            // Role 3 can get projects BUT only for repos they belong to
            ["ProjectList"] = new SyncKeyRule
            {
                AllowedRoles = AppRoles.All,
                IsRepoScoped = true,
                RepoParamKey = "repoId"
            },

            // All roles can view employees
            ["EmployeeList"] = new SyncKeyRule
            {
                AllowedRoles = AppRoles.All,
                IsRepoScoped = false
            },

            // All roles can view labels
            ["LabelMaster"] = new SyncKeyRule
            {
                AllowedRoles = AppRoles.All,
                IsRepoScoped = false
            },
        };
    }
}
