using APIGateWay.ModalLayer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.BusinessLayer.Auth
{
    public sealed class RouteRule
    {
        public string PathPrefix { get; init; } = string.Empty;
        public string[] Methods { get; init; } = { "*" };
        public int[] AllowedRoles { get; init; } = AppRoles.All;
        public bool ValidateRepoScope { get; init; }
    }

    public static class RouteAccessPolicy
    {
        public static readonly IReadOnlyList<RouteRule> Rules = new List<RouteRule>
        {
            // ── Auth — always open ────────────────────────────────────────────
            new() { PathPrefix = "/api/login",
                    Methods = new[] { "*" },       AllowedRoles = AppRoles.All,          ValidateRepoScope = false },

            // ── Sync — key-level filtering done by SyncRequestEnricher ────────
            new() { PathPrefix = "/api/sync",
                    Methods = new[] { "POST" },    AllowedRoles = AppRoles.All,          ValidateRepoScope = false },

            // ── Repository ────────────────────────────────────────────────────
            new() { PathPrefix = "/api/repo",
                    Methods = new[] { "GET" },     AllowedRoles = AppRoles.AdminManager, ValidateRepoScope = true  },
            new() { PathPrefix = "/api/repo",
                    Methods = new[] { "POST","PUT","DELETE" }, AllowedRoles = AppRoles.AdminOnly, ValidateRepoScope = false },

            // ── Project ───────────────────────────────────────────────────────
            new() { PathPrefix = "/api/project",
                    Methods = new[] { "GET" },     AllowedRoles = AppRoles.All,          ValidateRepoScope = true  },
            new() { PathPrefix = "/api/project",
                    Methods = new[] { "POST" },    AllowedRoles = AppRoles.All,          ValidateRepoScope = true  },
            new() { PathPrefix = "/api/project",
                    Methods = new[] { "PUT","DELETE" }, AllowedRoles = AppRoles.AdminManager, ValidateRepoScope = false },

            // ── Ticket ────────────────────────────────────────────────────────
            new() { PathPrefix = "/api/ticket",
                    Methods = new[] { "GET" },     AllowedRoles = AppRoles.All,          ValidateRepoScope = true  },
            new() { PathPrefix = "/api/ticket",
                    Methods = new[] { "POST" },    AllowedRoles = AppRoles.All,          ValidateRepoScope = true  },
            new() { PathPrefix = "/api/ticket",
                    Methods = new[] { "PUT","DELETE" }, AllowedRoles = AppRoles.AdminManager, ValidateRepoScope = false },

            // ── Employee ──────────────────────────────────────────────────────
            new() { PathPrefix = "/api/employee",
                    Methods = new[] { "GET" },     AllowedRoles = AppRoles.AdminManager, ValidateRepoScope = false },
            new() { PathPrefix = "/api/employee",
                    Methods = new[] { "POST","PUT","DELETE" }, AllowedRoles = AppRoles.AdminOnly, ValidateRepoScope = false },

            // ── Label ─────────────────────────────────────────────────────────
            new() { PathPrefix = "/api/label",
                    Methods = new[] { "GET" },     AllowedRoles = AppRoles.AdminManager, ValidateRepoScope = false },
            new() { PathPrefix = "/api/label",
                    Methods = new[] { "POST","PUT","DELETE" }, AllowedRoles = AppRoles.AdminOnly, ValidateRepoScope = false },

            // ── Attachment ────────────────────────────────────────────────────
            new() { PathPrefix = "/api/attachment",
                    Methods = new[] { "POST" },    AllowedRoles = AppRoles.All,          ValidateRepoScope = false },
        };

        /// <summary>
        /// Returns the most specific matching rule for this path + method.
        /// Returns null when no rule found — middleware denies by default.
        /// </summary>
        public static RouteRule? Match(string path, string method)
        {
            var lp = path.ToLowerInvariant();
            var um = method.ToUpperInvariant();
            return Rules
                .Where(r =>
                    lp.StartsWith(r.PathPrefix.ToLowerInvariant(), StringComparison.Ordinal) &&
                    (r.Methods.Contains("*") || r.Methods.Contains(um)))
                .OrderByDescending(r => r.PathPrefix.Length)
                .FirstOrDefault();
        }
    }
}
