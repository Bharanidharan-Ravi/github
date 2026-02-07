using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WGAPP.ModelLayer.GithubModal.MasterData;

namespace WGAPP.DomainLayer.Interface.GithubInterface
{
    public interface IMasterDataService
    {
        Task<List<GetClients>> GetClients();
        Task<List<LabelMaster>> GetLabels();
    }
}
