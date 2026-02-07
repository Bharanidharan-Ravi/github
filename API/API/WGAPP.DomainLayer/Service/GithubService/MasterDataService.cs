using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WGAPP.DomainLayer.DBContext;
using WGAPP.DomainLayer.Interface.GithubInterface;
using WGAPP.DomainLayer.Service.CommonService;
using WGAPP.ModelLayer.GithubModal.MasterData;
using WGAPP.ModelLayer.GithubModal.ProjectModal;

namespace WGAPP.DomainLayer.Service.GithubService
{
    public class MasterDataService : IMasterDataService
    {
        private readonly WGAPPDbContext _context;
        private readonly WGAPPCommonService _wGAPPCommonService;
        private readonly ILoginContextService _loginService;

        public MasterDataService(WGAPPDbContext context, WGAPPCommonService wGAPPCommonService, ILoginContextService loginContext)
        {
            _context = context;
            _wGAPPCommonService = wGAPPCommonService;
            _loginService = loginContext;
        }

        public async Task<List<GetClients>> GetClients()
        {
            return await _context.clientMasters
               .Where(c => c.Status == "Active")
               .Select(c => new GetClients  // Projecting the data to a DTO
               {
                   Client_Id = c.Client_Id,
                   Client_Code = c.Client_Code,
                   Client_Name = c.Client_Name
               })
               .ToListAsync();
        }
        public async Task<List<LabelMaster>> GetLabels()
        {
            var parameters = new SqlParameter[]
            {
                new SqlParameter("@DatabaseName", _loginService.databaseName)
            };
            var Response = await _wGAPPCommonService.ExecuteGetItemAsyc<LabelMaster>("GETLABELMASTER", parameters);

            return Response;
        }
    }
}
