using APIGateWay.BusinessLayer.Interface;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.BusinessLayer.Repository
{
    public class RepoRepository : IRepoRepository
    {
        private readonly IRepoService _repoService;
        public RepoRepository(IRepoService repoService)
        {
            _repoService = repoService;
        }

        public async Task<PostRepositoryModel> PostRepo(LoginMasterDto login, ClientMasterDto clientMaster, PostRepositoryModel repo)
        {
            var response = await _repoService.PostRepo(login, clientMaster, repo);
            return response;
        }
    }
}
