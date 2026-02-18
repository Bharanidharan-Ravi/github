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

        public async Task<string> PostRepo(PostRepoDto repo)
        {
            var response = await _repoService.PostRepo(repo);
            return response;
        }
    }
}
