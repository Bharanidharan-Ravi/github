// APIGateWay.BusinessLayer.Auth.SyncRequestEnricher
//
// ──────────────────────────────────────────────────────────────────────────────
// HOW IT WORKS
//
// Role 1 / 2  → pass through unchanged. No DB call. No modification.
//
// Role 3      → For each config key in the request:
//
//   CASE A: Key is blocked (e.g. "RepoList")
//     → Add to DeniedKeys immediately. No execution.
//
//   CASE B: Key is repo-scoped (e.g. "TicketsList", "ProjectList")
//     → Fetch the user's repo list ONCE (cached for the request)
//     → Fan out: create one SyncExecutionUnit per repo
//     → Each unit has repoId injected into its Params automatically
//     → Frontend never needs to send repoId — API injects it transparently
//
//   CASE C: Key is not repo-scoped (e.g. "EmployeeList", "LabelMaster")
//     → Pass through normally
//
// ──────────────────────────────────────────────────────────────────────────────

using APIGateWay.BusinessLayer.Auth;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer;
using APIGateWay.ModalLayer.nugetmodal;
using Microsoft.AspNetCore.Http;

namespace APIGateWay.BusinessLayer.Auth
{
    public class SyncRequestEnricher : ISyncRequestEnricher
    {
        private readonly ILoginContextService _login;
        private readonly IHttpContextAccessor _httpCtx;

        public SyncRequestEnricher(ILoginContextService login, IHttpContextAccessor httpCtx)
        {
            _login = login;
            _httpCtx = httpCtx;
        }

        public async Task<EnrichedSyncRequest> EnrichAsync(DynamicSyncRequest request)
        {
            var enriched = new EnrichedSyncRequest();
            var role = _login.role;

            // ── Role 1: unrestricted pass-through ─────────────────────────────
            if (role == AppRoles.Admin || role == AppRoles.Manager)
            {
                foreach (var key in request.ConfigKeys)
                {
                    request.Timestamps.TryGetValue(key, out var ts);
                    request.Params.TryGetValue(key, out var p);
                    enriched.Units.Add(Unit(key, ts, p ?? new()));
                }
                return enriched;
            }

            // ── Role 2 / 3: read repo cache set by middleware ─────────────────
            var allowedRepos = _httpCtx.HttpContext?.Items["AllowedRepos"]
                as List<UserRepoAccess>
                ?? new List<UserRepoAccess>();

            foreach (var key in request.ConfigKeys)
            {
                request.Timestamps.TryGetValue(key, out var lastSync);
                request.Params.TryGetValue(key, out var baseParams);
                baseParams ??= new Dictionary<string, string>();

                // Unknown key — let SyncRepositoryV2 return INVALID_CONFIG_KEY
                if (!SyncKeyPolicy.Rules.TryGetValue(key, out var rule))
                {
                    enriched.Units.Add(Unit(key, lastSync, baseParams));
                    continue;
                }

                // Role not in allowed list for this key
                if (!rule.AllowedRoles.Contains(role))
                {
                    enriched.DeniedKeys[key] = $"Your role does not have access to '{key}'.";
                    continue;
                }

                // Repo-scoped → fan out one unit per allowed repo
                if (rule.IsRepoScoped)
                {
                    if (!allowedRepos.Any())
                    {
                        enriched.DeniedKeys[key] = "You have not been assigned to any repository.";
                        continue;
                    }

                    foreach (var repo in allowedRepos)
                    {
                        var unitParams = new Dictionary<string, string>(baseParams)
                        {
                            [rule.RepoParamKey] = repo.RepoId.ToString()
                        };
                        enriched.Units.Add(Unit(key, lastSync, unitParams));
                    }
                    continue;
                }

                // Not repo-scoped — pass through
                enriched.Units.Add(Unit(key, lastSync, baseParams));
            }

            return enriched;
        }

        private static SyncExecutionUnit Unit(
            string key, DateTimeOffset? ts, Dictionary<string, string> p) =>
            new() { ConfigKey = key, ResultKey = key, LastSync = ts, Params = p };
    }
}