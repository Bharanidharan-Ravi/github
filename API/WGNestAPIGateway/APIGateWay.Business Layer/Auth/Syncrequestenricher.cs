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

using APIGateWay.Business_Layer.Auth;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer;
using APIGateWay.ModalLayer.nugetmodal;

namespace APIGateWay.BusinessLayer.Auth
{
    public class SyncRequestEnricher : ISyncRequestEnricher
    {
        private readonly ILoginContextService _loginCtx;
        private readonly IRepoAccessService _repoAccess;

        public SyncRequestEnricher(
            ILoginContextService loginCtx,
            IRepoAccessService repoAccess)
        {
            _loginCtx = loginCtx;
            _repoAccess = repoAccess;
        }

        public async Task<EnrichedSyncRequest> EnrichAsync(DynamicSyncRequest request)
        {
            var enriched = new EnrichedSyncRequest();
            var userRole = _loginCtx.role;

            // ── Roles 1 and 2: pass every key through unchanged ───────────────
            if (userRole == AppRoles.Admin || userRole == AppRoles.Manager)
            {
                foreach (var key in request.ConfigKeys)
                {
                    request.Timestamps.TryGetValue(key, out var ts);
                    request.Params.TryGetValue(key, out var p);

                    enriched.Units.Add(new SyncExecutionUnit
                    {
                        ConfigKey = key,
                        ResultKey = key,
                        LastSync = ts,
                        Params = p ?? new Dictionary<string, string>()
                    });
                }
                return enriched;
            }

            // ── Role 3: fetch their repos once, then fan out ──────────────────
            // GetUserRepoIdsAsync returns the RepoKey list (used by SignalR groups).
            // We also need Repo_Id GUIDs for the SP params.
            // Fetch both in one call using the extended service.
            var allowedRepoIds = await _repoAccess.GetUserRepoGuidsAsync(_loginCtx.userId);
            // Returns List<UserRepoAccess> with both RepoId (GUID) and RepoKey

            foreach (var key in request.ConfigKeys)
            {
                // Look up the policy rule for this key
                if (!SyncKeyPolicy.Rules.TryGetValue(key, out var rule))
                {
                    // Unknown key — pass through, let SyncRepositoryV2 handle the error
                    request.Timestamps.TryGetValue(key, out var ts);
                    request.Params.TryGetValue(key, out var p);
                    enriched.Units.Add(new SyncExecutionUnit
                    {
                        ConfigKey = key,
                        ResultKey = key,
                        LastSync = ts,
                        Params = p ?? new()
                    });
                    continue;
                }

                // ── CASE A: Role 3 not allowed for this key at all ────────────
                if (!rule.AllowedRoles.Contains(userRole))
                {
                    enriched.DeniedKeys[key] =
                        $"Your role does not have access to '{key}'.";
                    continue;
                }

                request.Timestamps.TryGetValue(key, out var lastSync);
                request.Params.TryGetValue(key, out var baseParams);
                baseParams ??= new Dictionary<string, string>();

                // ── CASE B: Repo-scoped — fan out per repo ────────────────────
                if (rule.IsRepoScoped)
                {
                    if (allowedRepoIds == null || !allowedRepoIds.Any())
                    {
                        // User has no repo access at all
                        enriched.DeniedKeys[key] =
                            "You have not been assigned to any repository.";
                        continue;
                    }

                    foreach (var repo in allowedRepoIds)
                    {
                        // Clone base params, inject repoId for this repo
                        var unitParams = new Dictionary<string, string>(baseParams)
                        {
                            // Overwrite/add the repoId param the SP expects
                            [rule.RepoParamKey] = repo.RepoId.ToString()
                        };

                        enriched.Units.Add(new SyncExecutionUnit
                        {
                            ConfigKey = key,
                            ResultKey = key,          // results merged under the same key
                            LastSync = lastSync,
                            Params = unitParams
                        });
                    }
                    continue;
                }

                // ── CASE C: Not repo-scoped — pass through normally ───────────
                enriched.Units.Add(new SyncExecutionUnit
                {
                    ConfigKey = key,
                    ResultKey = key,
                    LastSync = lastSync,
                    Params = baseParams
                });
            }

            return enriched;
        }
    }
}