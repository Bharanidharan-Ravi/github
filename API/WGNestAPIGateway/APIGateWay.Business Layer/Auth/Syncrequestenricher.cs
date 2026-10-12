// APIGateWay.BusinessLayer.Auth.SyncRequestEnricher
//
// ──────────────────────────────────────────────────────────────────────────────
// HOW IT WORKS
//
// Role 1 / 2  → pass through unchanged. No DB call. No modification.
//
// Role 3      → For each config key in the request:
//
//   CASE A: Key not in SyncKeyPolicy.Rules, or role not allowed (e.g. "EmployeeList")
//     → Add to DeniedKeys immediately. No execution. (deny by default)
//
//   CASE B: Key is ticket-scoped (e.g. "ThreadsList", "TicketHistory")
//     → The ticket id param is required and the ticket must be in one of
//       the user's repos, otherwise denied
//
//   CASE C: Key is repo-scoped (e.g. "TicketsList", "ProjectList")
//     → Fetch the user's repo list ONCE (cached for the request)
//     → Fan out: create one SyncExecutionUnit per repo
//     → Each unit has repoId injected into its Params automatically
//     → Frontend never needs to send repoId — API injects it transparently
//     → A repoId the frontend does send must be one of theirs; only it runs
//
//   CASE D: Key is listed but not scoped (e.g. "LabelMaster")
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
        private readonly IRepoAccessService _repoAccess;

        public SyncRequestEnricher(ILoginContextService login, IHttpContextAccessor httpCtx, IRepoAccessService repoAccess)
        {
            _login = login;
            _httpCtx = httpCtx;
            _repoAccess = repoAccess;
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

                    // 🔥 Inject role for ThreadsList even for Admin/Manager if needed
                    if (key == "ThreadsList" || key == "TicketsList")
                    {
                        p ??= new Dictionary<string, string>();
                        p["Role"] = role.ToString();
                    }

                    enriched.Units.Add(Unit(key, ts, p ?? new()));
                }
                return enriched;
            }

            // ── Role 3: read repo cache set by middleware ─────────────────────
            var allowedRepos = _httpCtx.HttpContext?.Items["AllowedRepos"]
                as List<UserRepoAccess>
                ?? new List<UserRepoAccess>();
            var ticketAccess = new Dictionary<Guid, bool>(); // per request: ticket → allowed

            foreach (var key in request.ConfigKeys)
            {
                request.Timestamps.TryGetValue(key, out var lastSync);
                request.Params.TryGetValue(key, out var baseParams);
                baseParams ??= new Dictionary<string, string>();

                // Deny by default — a key without a rule is Admin / Manager only
                if (!SyncKeyPolicy.Rules.TryGetValue(key, out var rule) ||
                    !rule.AllowedRoles.Contains(role))
                {
                    enriched.DeniedKeys[key] = $"Your role does not have access to '{key}'.";
                    continue;
                }

                if (key == "ThreadsList" || key == "TicketsList")
                    baseParams["Role"] = role.ToString();

                // Ticket-scoped → the requested ticket must be in one of the user's repos
                if (rule.TicketParamKey != null &&
                    !await CanReadTicketAsync(baseParams, rule.TicketParamKey, allowedRepos, ticketAccess))
                {
                    enriched.DeniedKeys[key] = "You do not have access to this ticket.";
                    continue;
                }

                if (!rule.IsRepoScoped)
                {
                    enriched.Units.Add(Unit(key, lastSync, baseParams));
                    continue;
                }

                // Repo-scoped
                if (!allowedRepos.Any())
                {
                    enriched.DeniedKeys[key] = "You have not been assigned to any repository.";
                    continue;
                }

                // One unit with every repo (paged / counted SPs)
                if (rule.RepoListParamKey != null)
                {
                    var unitParams = new Dictionary<string, string>(baseParams)
                    {
                        [rule.RepoListParamKey] = string.Join(",", allowedRepos.Select(r => r.RepoId))
                    };
                    enriched.Units.Add(Unit(key, lastSync, unitParams));
                    continue;
                }

                // A repo the caller asked for must be theirs; then only that one runs
                var repos = allowedRepos;
                if (baseParams.TryGetValue(rule.RepoParamKey, out var requested) &&
                    !string.IsNullOrWhiteSpace(requested))
                {
                    repos = allowedRepos
                        .Where(r => Guid.TryParse(requested, out var id) && r.RepoId == id)
                        .ToList();

                    if (!repos.Any())
                    {
                        enriched.DeniedKeys[key] = "You do not have access to this repository.";
                        continue;
                    }
                }

                // Fan out one unit per repo
                foreach (var repo in repos)
                {
                    var unitParams = new Dictionary<string, string>(baseParams)
                    {
                        [rule.RepoParamKey] = repo.RepoId.ToString()
                    };
                    enriched.Units.Add(Unit(key, lastSync, unitParams));
                }
            }

            return enriched;
        }

        private async Task<bool> CanReadTicketAsync(
            Dictionary<string, string> p, string ticketParamKey,
            List<UserRepoAccess> allowedRepos, Dictionary<Guid, bool> checkedTickets)
        {
            if (!p.TryGetValue(ticketParamKey, out var raw) || !Guid.TryParse(raw, out var ticketId))
                return false;

            if (!checkedTickets.TryGetValue(ticketId, out var allowed))
            {
                var repoKey = await _repoAccess.GetTicketRepoKeyAsync(ticketId);
                allowed = repoKey != null && allowedRepos.Any(r =>
                    string.Equals(r.RepoKey, repoKey, StringComparison.OrdinalIgnoreCase));
                checkedTickets[ticketId] = allowed;
            }
            return allowed;
        }

        private static SyncExecutionUnit Unit(
            string key, DateTimeOffset? ts, Dictionary<string, string> p) =>
            new() { ConfigKey = key, ResultKey = key, LastSync = ts, Params = p };
    }
}