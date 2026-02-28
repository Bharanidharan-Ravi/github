using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.DomainLayer.Interface
{
    public interface IRepoScopeValidator
    {
        /// <summary>
        /// Returns true if the current user has access to the given repoId.
        /// Roles 1 and 2 always return true (no scope restriction).
        /// Role 3 is checked against RepoUsers table via RepoAccessService.
        /// </summary>
        Task<bool> CanAccessRepoAsync(string repoId);

        /// <summary>
        /// Returns all repoIds the current user can access.
        /// Roles 1 and 2 return null (= unrestricted, caller fetches all).
        /// Role 3 returns their specific list from RepoUsers.
        /// </summary>
        Task<List<string>?> GetAllowedRepoIdsAsync();
    }
}
