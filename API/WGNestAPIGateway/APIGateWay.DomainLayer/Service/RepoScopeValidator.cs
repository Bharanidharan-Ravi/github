using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.DomainLayer.Service
{
    public class RepoScopeValidator : IRepoScopeValidator
    {
        private readonly ILoginContextService _loginCtx;
        private readonly IRepoAccessService _repoAccess;

        // Cache per request — GetUserRepoIdsAsync is called at most once per request
        private List<string>? _cachedRepoIds;
        private bool _cacheLoaded;

        public RepoScopeValidator(
            ILoginContextService loginCtx,
            IRepoAccessService repoAccess)
        {
            _loginCtx = loginCtx;
            _repoAccess = repoAccess;
        }

        //public async Task<bool> CanAccessRepoAsync(string repoId)
        //{
        //    // Roles 1 and 2 — unrestricted
        //    if (_loginCtx.role == AppRoles.Admin || _loginCtx.role == AppRoles.Manager)
        //        return true;

        //    // Role 3 — must be in their repo list
        //    var allowed = await GetAllowedRepoIdsAsync();
        //    return allowed != null && allowed.Contains(repoId);
        //}
        /// <summary>
        /// Called by SyncRoleGuard with the repoId string from request.Params.
        ///
        /// Flow:
        ///   Frontend URL:  /repository/abc-123-def  → useParams() → repoId = "abc-123-def"
        ///   Sync request:  Params["TicketsList"]["repoId"] = "abc-123-def"
        ///   Guard calls:   CanAccessRepoAsync("abc-123-def")
        ///   This method:   parses "abc-123-def" as Guid, queries RepoUsers by Repo_Id
        /// </summary>
        //public async Task<bool> CanAccessRepoAsync(string repoIdString)
        //{
        //    // Roles 1 and 2 — always allowed, no DB call
        //    if (_loginCtx.role == AppRoles.Admin || _loginCtx.role == AppRoles.Manager)
        //        return true;

        //    // Role 3 — parse the GUID from the string the frontend sent
        //    if (!Guid.TryParse(repoIdString, out var repoGuid))
        //    {
        //        // Value is not a valid GUID — deny
        //        return false;
        //    }

        //    // Check RepoUsers WHERE UserId = current user AND RepoId = requested repo
        //    return await _repoAccess.UserCanAccessRepoByIdAsync(_loginCtx.userId, repoGuid);
        //}
        /// <summary>
        /// Returns the list of repoId strings Role 3 can access.
        /// Not used by SyncRoleGuard (it calls CanAccessRepoAsync directly)
        /// but available for other service-layer checks.
        /// </summary>
        public async Task<List<string>?> GetAllowedRepoIdsAsync()
        {
            if (_loginCtx.role == AppRoles.Admin || _loginCtx.role == AppRoles.Manager)
                return null; // null = unrestricted

            var keys = await _repoAccess.GetUserRepoIdsAsync(_loginCtx.userId);
            return keys;
        }

        //public async Task<List<string>?> GetAllowedRepoIdsAsync()
        //{
        //    // Roles 1 and 2 — null means "unrestricted, fetch everything"
        //    if (_loginCtx.role == AppRoles.Admin || _loginCtx.role == AppRoles.Manager)
        //        return null;

        //    // Role 3 — lazy-load once per request
        //    if (!_cacheLoaded)
        //    {
        //        _cachedRepoIds = await _repoAccess.GetUserRepoIdsAsync(_loginCtx.userId);
        //        _cacheLoaded = true;
        //    }
        //    return _cachedRepoIds;
    }
}
