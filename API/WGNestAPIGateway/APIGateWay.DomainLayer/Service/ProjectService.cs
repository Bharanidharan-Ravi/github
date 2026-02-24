using APIGateWay.DomainLayer.CommonSevice;
using APIGateWay.DomainLayer.DBContext;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.GETData;
using APIGateWay.ModalLayer.MasterData;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static APIGateWay.ModalLayer.Helper.HelperModal;

namespace APIGateWay.DomainLayer.Service
{
    public class ProjectService
    {
        private readonly ILoginContextService _loginContextService;
        private readonly APIGatewayDBContext _context;
        private readonly APIGateWayCommonService _commonService;
        public ProjectService(ILoginContextService loginContextService, APIGatewayDBContext dBContext, APIGateWayCommonService commonService)
        {
            _loginContextService = loginContextService;
            _context = dBContext;
            _commonService = commonService;
        }

    //    #region Post project on master table 
    //    public async Task<GetProject> PostProject(ProjectMaster project)
    //    {
    //        try
    //        {
    //            #region Get project Sequence

    //            string numberKey = project.RepoKey;
    //            string columnName = "Project";
    //            string seriesName = "Proj_Sequence";

    //            var pSeriesName = new SqlParameter[]
    //            {
    //               new SqlParameter("@SeriesName", numberKey),
    //               new SqlParameter("@ColumnName", columnName),
    //               new SqlParameter("@CurrentSeriesName", seriesName)
    //            };

    //            var nextSeq = await _commonService
    //                .ExecuteGetItemAsyc<SequenceResult>(
    //                    "GetNextNumber",
    //                    pSeriesName
    //                );

    //            int sino = nextSeq[0].CurrentValue;
    //            int? columnValue = nextSeq[0].ColumnValue;
    //            #endregion
    //            project.Status = 1;
    //            project.SiNo = sino;
    //            project.ProjectKey = $"P{columnValue}";

    //            _context.ProjectMasters.Add(project);
    //            // Save changes to the database
    //            var response = await _context.SaveChangesAsync();

    //            Guid? ProjId = project.Id;
    //            var data = await GetProjMaster(ProjId: ProjId);
    //            return data[0]; // Return the created project object
    //        }
    //        catch (Exception ex)
    //        {
    //            throw new Exception("error while creating project ", ex);
    //        }
    //    }
    //    #endregion
    }
}
