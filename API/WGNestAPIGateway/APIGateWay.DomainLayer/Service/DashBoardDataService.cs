using APIGateWay.DomainLayer.CommonSevice;
using APIGateWay.DomainLayer.DBContext;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.GETData;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging; // <-- Add this namespace for ILogger
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.DomainLayer.Service
{
    public class DashBoardDataService : IDashBoardDataService
    {
        private readonly APIGateWayCommonService _commonService;
        private readonly ILoginContextService _loginContextService;
        private readonly APIGatewayDBContext _gatewayDBContext;
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;


        public DashBoardDataService(APIGateWayCommonService commonService, APIGatewayDBContext gatewayDBContext, IConfiguration configuration, IHttpContextAccessor httpContextAccessor, ILoginContextService loginContextService)
        {
            _commonService = commonService;
            _gatewayDBContext = gatewayDBContext;
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
            _loginContextService = loginContextService;
        }

        public async Task<DashBoard> GetDashBoardData(Guid? employeeId = null, DateTime? perDay = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            
            var userId = employeeId.HasValue && employeeId.Value != Guid.Empty
                   ? employeeId.Value
                   : _loginContextService.userId;

            var parameters = new SqlParameter[]
            {
               new SqlParameter("@EmployeeID", SqlDbType.UniqueIdentifier) { Value = userId },
               new SqlParameter("@Perday", SqlDbType.Date) { Value = (object)perDay ?? DBNull.Value },
               new SqlParameter("@FromDate", SqlDbType.Date) { Value = (object)fromDate ?? DBNull.Value },
               new SqlParameter("@Todate", SqlDbType.Date) { Value = (object)toDate ?? DBNull.Value }
            };

            var dataset = await _commonService.ExecuteReturnAsync("DashBoardData", parameters);

            var dashBoardData = dataset.Tables[0].AsEnumerable().Select(row => row.AutoCast<GetDashBoardData>()).ToList();
            var ticketCount = dataset.Tables[1].AsEnumerable().Select(row => row.AutoCast<Count>()).ToList();
            

            return new DashBoard
            {
                DashBoardData = dashBoardData,
                TicketCount = ticketCount
               
            };
        }

    }
}


