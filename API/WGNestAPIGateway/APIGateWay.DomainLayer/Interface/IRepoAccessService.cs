using System;

namespace APIGateWay.DomainLayer.Interface
{

    public interface IRepoAccessService
    {
        /// <summary>RepoKey strings for SignalR hub group assignment.</summary>
        Task<List<string>> GetUserRepoIdsAsync(Guid userId);

        /// <summary>
        /// Both RepoId GUID and RepoKey for every repo the user belongs to.
        /// Called ONCE per request by RoleBasedAccessMiddleware.
        /// Result cached in context.Items["AllowedRepos"] —
        /// SyncRequestEnricher reads it without a second DB call.
        /// </summary>
        Task<List<UserRepoAccess>> GetUserRepoGuidsAsync(Guid userId);
    }

    /// <summary>Both identifiers of a repo the user is assigned to.</summary>
    public sealed class UserRepoAccess
    {
        /// <summary>Repo_Id GUID from RepositoryMasters — passed to stored procedures.</summary>
        public Guid RepoId { get; init; }
        /// <summary>Short alphanumeric key — used for SignalR groups.</summary>
        public string RepoKey { get; init; } = string.Empty;
    }
}
