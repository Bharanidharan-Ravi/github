using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WGAPP.BusinessLayer.Hub;
using WGAPP.BusinessLayer.Interface.GithubInterface;
using WGAPP.DomainLayer.Interface.GithubInterface;
using WGAPP.ModelLayer.GithubModal.RepositoryModal;
using WGAPP.ModelLayer.GithubModal.TicketingModal;

namespace WGAPP.BusinessLayer.Repository.GithubRepository
{
    public class RepositoryRepo : IRepositoryInterface
    {
        private readonly IRepositoryService _RepositoryService;
        private readonly INotificationService _notificationService;

        public RepositoryRepo(IRepositoryService repositoryService,INotificationService notificationService)
        {
            _RepositoryService = repositoryService;
            _notificationService = notificationService;
        }

        #region Getting all Repo for employee login 
        public async Task<List<RepoData>> GetRepoData(string clientId = null)
        {
            var response = await _RepositoryService.GetRepoData(clientId);
            return response;
        }
        #endregion

        #region Post Repository by Client id
        public async Task<RepoData> InsertOrUpdateRepository(PostRepositoryModel data, string DbName)
        {
            var result =  await _RepositoryService.InsertOrUpdateRepository(data, DbName);
            //var link = new TicketHubModal
            //{
            //    Id = issue.Issue_Id,
            //    Title = issue.Description
            //};

            await _notificationService.RepoCreated(result);
            return result;

        }
        #endregion
    }
}
