using System;

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
        string JwtToken { get; }
        string RequestPath { get; }
    }
}
