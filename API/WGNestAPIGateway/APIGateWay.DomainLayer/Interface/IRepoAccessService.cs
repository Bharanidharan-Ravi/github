using System;

namespace APIGateWay.DomainLayer.Interface
{
    public interface IRepoAccessService
    {
        // ── Already exists — used by SignalR hub ──────────────────────────────
        Task<List<string>> GetUserRepoIdsAsync(Guid userId);

        // ── ADD THIS ──────────────────────────────────────────────────────────
        // Returns both the Repo_Id GUID (for SP params) and RepoKey (for other uses).
        // Called once per request by SyncRequestEnricher, result is used to
        // fan out one execution unit per repo for Role 3.
        Task<List<UserRepoAccess>> GetUserRepoGuidsAsync(Guid userId);
        Task<bool> UserCanAccessRepoByIdAsync(Guid userId, Guid repoGuid);
    }

    /// <summary>
    /// Holds both identifiers for a repo the user belongs to.
    /// RepoId  = the GUID primary key (Repo_Id column) — passed to SPs as @repoId
    /// RepoKey = the short string key                  — used for SignalR groups
    /// </summary>
    public class UserRepoAccess
    {
        public Guid RepoId { get; set; }
        public string RepoKey { get; set; }
    }
}
