using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WGAPP.DomainLayer.DBContext;
using WGAPP.DomainLayer.ErrorException;
using WGAPP.DomainLayer.Interface;
using WGAPP.DomainLayer.Interface.GithubInterface;
using WGAPP.DomainLayer.Service.CommonService;
using WGAPP.ModelLayer.GithubModal.ProjectModal;
using WGAPP.ModelLayer.GithubModal.RepositoryModal;

namespace WGAPP.DomainLayer.Service.GithubService
{
    public class ProjectService : IProjectService
    {
        private readonly WGAPPDbContext _context;
        private readonly ILoginContextService _loginContextService;
        private readonly WGAPPCommonService _wGAPPCommonService;
        public ProjectService(WGAPPDbContext wGAPPDbContext, ILoginContextService loginContextService, WGAPPCommonService wGAPPCommonService) 
        { 
            _context = wGAPPDbContext;
            _loginContextService = loginContextService;
            _wGAPPCommonService = wGAPPCommonService;
        }

        #region Get a project master 
        public async Task<List<GetProject>> GetProjMaster (Guid? clientId = null, Guid? repoId = null, Guid? ProjId = null)
        {
            var parameters = new SqlParameter[]
            {
                new SqlParameter("@DatabaseName", _loginContextService.databaseName),
                new SqlParameter("@ClientId", clientId ?? (object)DBNull.Value), 
                new SqlParameter("@RepoId", repoId ?? (object)DBNull.Value),
                new SqlParameter("@ProjId", ProjId ?? (object)DBNull.Value),
            };
            var IssuesData = await _wGAPPCommonService.ExecuteGetItemAsyc<GetProject>("GETALLPROJECTDATA", parameters);
            if (IssuesData == null || IssuesData.Count == 0)
            {
                throw new Exceptionlist.DataNotFoundException("No data found for the provided parameters.");
            }
            return IssuesData;
        }
        #endregion

        #region Post project on master table 
        public async Task<GetProject> PostProject (ProjectMaster project)
        {
            // Set the created and updated date/time
            project.Created_On = DateTime.Now;
            //project.UpdatedOn = DateTime.Now;

            //// Set the ID to a new GUID if not provided
            project.Created_By =_loginContextService.userId;

            // Set default values for status and other fields if necessary
            project.Status = "Active"; 
            // Add the project to the context

            try
            {
                _context.PROJECTMASTER.Add(project);
                // Save changes to the database
                var response =  await _context.SaveChangesAsync();

                Guid? ProjId = project.Id;
                var data = await GetProjMaster(ProjId: ProjId);
                return data[0]; // Return the created project object
            }
            catch (Exception ex)
            {
                throw new Exception("error while creating project ", ex);
            }
        }
        #endregion
    }
}
