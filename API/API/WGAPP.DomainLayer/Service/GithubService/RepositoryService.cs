using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WGAPP.DomainLayer.DBContext;
using WGAPP.DomainLayer.ErrorException;
using WGAPP.DomainLayer.Service.CommonService;
using WGAPP.ModelLayer.GithubModal.ViewIssues;
using WGAPP.ModelLayer.GithubModal.RepositoryModal;
using WGAPP.DomainLayer.Interface.GithubInterface;
using Microsoft.EntityFrameworkCore;
using SAPbobsCOM;

namespace WGAPP.DomainLayer.Service.GithubService
{
    public class RepositoryService : IRepositoryService
    {
        private readonly WGAPPDbContext _context;
        private readonly WGAPPCommonService _wGAPPCommonService;
        private readonly ILoginContextService _loginService;
        public RepositoryService(WGAPPDbContext context, WGAPPCommonService wGAPPCommonService, ILoginContextService loginContext)
        {
            _context = context;
            _wGAPPCommonService = wGAPPCommonService;
            _loginService = loginContext;
        }

        #region Getting all Repo for employee login 
        public async Task<List<RepoData>> GetRepoData(string clientId = null)
        {
            var parameters = new SqlParameter[]
            {
                new SqlParameter("@DatabaseName", _loginService.databaseName),
                new SqlParameter("@ClientId", clientId ?? (object)DBNull.Value)
            };
            var IssuesData = await _wGAPPCommonService.ExecuteGetItemAsyc<RepoData>("GETALLREPO", parameters);
            if (IssuesData == null || IssuesData.Count == 0)
            {
                throw new Exceptionlist.DataNotFoundException("No data found for the provided parameters.");
            }
            return IssuesData;
        }
        #endregion

        #region Post Repository data
        public async Task<RepoData> InsertOrUpdateRepository(PostRepositoryModel data, string DbName)
        {
            // Track the current UTC time before the insert operation
            var currentUtcTime = DateTime.UtcNow;

            try
            {
                data.Created_On = currentUtcTime;
                data.Status = "Active";
                //var repo = new PostRepositoryModel
                //{
                //    Title = data.Title,
                //    Description = data.Description,
                //    Repo_Code = data.Repo_Code,
                //    Client_Id = data.Client_Id,
                //    Created_On = DateTime.UtcNow,
                //    Created_By = data.Created_By,
                //    Status = "Active"
                //};
                _context.REPOSITORIES.Add(data);
                // Save the changes to the database
                await _context.SaveChangesAsync();
                Guid? repoId = data.Repo_Id;
                // After saving, fetch the records that were inserted around the same time
                //var insertedItems = await _context.REPOSITORIES
                //    .Where(r => r.Created_On == currentUtcTime)  // Filter by the same UTC time
                //    .ToListAsync();

                var parameters = new SqlParameter[]
            {
                new SqlParameter("@DatabaseName", DbName),
                new SqlParameter("@RepoId", repoId ?? (object)DBNull.Value)
            };
                var repoData = await _wGAPPCommonService
         .ExecuteGetItemAsyc<RepoData>("GETALLREPO", parameters);

                return repoData[0];
            }
            catch (Exception ex)
            {
                throw new Exceptionlist.InvalidDataException(ex.Message);
            }
        }

        #endregion
    }
}
