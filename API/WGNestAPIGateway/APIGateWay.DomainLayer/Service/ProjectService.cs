using APIGateWay.DomainLayer.DBContext;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.GETData;
using APIGateWay.ModalLayer.MasterData;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.DomainLayer.Service
{
    public class ProjectService
    {
        private readonly ILoginContextService _loginContextService;
        private readonly APIGatewayDBContext _context;
        public ProjectService(ILoginContextService loginContextService, APIGatewayDBContext dBContext)
        {
            _loginContextService = loginContextService;
            _context = dBContext;
        }

        #region Post project on master table 
        //public async Task<GetProject> PostProject(ProjectMaster project)
        //{
        //    project.Status = "Active";
        //    try
        //    {
        //        var numberKey = d
        //        _context.PROJECTMASTER.Add(project);
        //        // Save changes to the database
        //        var response = await _context.SaveChangesAsync();

        //        Guid? ProjId = project.Id;
        //        var data = await GetProjMaster(ProjId: ProjId);
        //        return data[0]; // Return the created project object
        //    }
        //    catch (Exception ex)
        //    {
        //        throw new Exception("error while creating project ", ex);
        //    }
        //}
        #endregion
    }
}
