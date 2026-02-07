using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WGAPP.ModelLayer.GithubModal.RepositoryModal;

namespace WGAPP.BusinessLayer.Interface.GithubInterface
{
    public interface IRepositoryInterface
    {
        Task<List<RepoData>> GetRepoData(string clientId = null);
        Task<RepoData> InsertOrUpdateRepository(PostRepositoryModel data, string DbName);
    }
}
