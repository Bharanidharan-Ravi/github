using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WGAPP.ModelLayer.GithubModal.RepositoryModal;

namespace WGAPP.DomainLayer.Interface.GithubInterface
{
    public interface IRepositoryService 
    {
        Task<List<RepoData>> GetRepoData(string clientId = null);
        //Task<PostRepositoryModel> InsertOrUpdateRepository(PostRepositoryModel data);
        Task<RepoData> InsertOrUpdateRepository(PostRepositoryModel data, string DbName);
    }
}
