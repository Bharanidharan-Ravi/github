using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WGAPP.BusinessLayer.Interface.GithubInterface;
using WGAPP.DomainLayer.Interface.GithubInterface;
using WGAPP.ModelLayer.GithubModal.MasterData;

namespace WGAPP.BusinessLayer.Repository.GithubRepository
{
    public class MasterDataRepo : IMasterDataRepo
    {
        private readonly IMasterDataService _masterDataService;
        public MasterDataRepo(IMasterDataService master) 
        {
            _masterDataService = master;
        }

        public Task<List<GetClients>> GetClients()
        {
            var response = _masterDataService.GetClients();
            return response;
        }
        public Task<List<LabelMaster>> GetLabels()
        {
            var response = _masterDataService.GetLabels();
            return response;
        }
    }
}
