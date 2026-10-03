using System;
using System.Collections.Generic;
using System.Linq;

namespace APIGateWay.DomainLayer.Interface
{
    public interface ILoginContextService
    {
        Guid userId { get; }
        string userName { get; }
        string databaseName { get; }
        string Status { get; }
        // ── ADD THIS ───────────────────────────────────────────────────
        /// <summary>
        /// Role from the decoded JWT. Matches AppRoles constants (1, 2, 3).
        /// Returns 0 if session is missing or role cannot be parsed.
        /// HttpContextMiddleware populates context.Items["UserDetail:Role"]
        /// on every request from the decoded token.
        /// </summary>
        int role { get; }

        /// <summary>
        /// Effective roles from the JWT "Roles" claim: the user's own role plus every
        /// parent in ROLESMASTER (e.g. Ticket Admin 4 → { 4, 2 }). Falls back to { role }.
        /// </summary>
        IReadOnlyList<int> roles => new[] { role };

        /// <summary>True when the user holds the role directly or through a child role.</summary>
        bool HasRole(int r) => roles.Contains(r);

        /// <summary>True when the user holds any of the given roles (use with AppRoles arrays).</summary>
        bool HasAnyRole(IEnumerable<int> allowed) => allowed.Any(HasRole);

        string JwtToken { get; }
        string RequestPath { get; }
    }
}
