//using APIGateWay.Business_Layer.Interface;
//using APIGateWay.BusinessLayer.Helpers;
//using APIGateWay.ModalLayer;
//using APIGateWay.ModalLayer.GETData;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.Extensions.Logging;

//namespace WGNestAPIGateway.Controllers
//{

//    [ApiController]
//    [Route("api/[controller]")]
//    public class DashBoardDataController : ControllerBase
//    {
//        private readonly IDashBoardDataRepo _dashBoardDataRepo;
//        private readonly ILogger<DashBoardDataController> _logger;

//        public DashBoardDataController(IDashBoardDataRepo dashBoardDataRepo)
//        {
//            _dashBoardDataRepo = dashBoardDataRepo;
//            _logger = logger;
//        }

//        //[HttpGet("DashBoardData")]
//        //public async Task<IActionResult> GetDashBoardData(
//        //    [FromQuery] Guid employeeId,
//        //    [FromQuery] DateTime? perDay = null,
//        //    [FromQuery] DateTime? fromDate = null,
//        //    [FromQuery] DateTime? toDate = null)
//        //{
//        //    var response = await _dashBoardDataRepo.GetDashBoardData(employeeId, perDay, fromDate, toDate);

//        //    return Ok(response);
//        //}

//        //[HttpPost("GetDashBoardData")]
//        //public async Task<ActionResult<DashBoard>> GetDashBoardData([FromBody] Dashboardget dash )
//        //{
//        //    var result = await _dashBoardDataRepo.GetDashBoardData(dash.employeeId, dash.perDay, dash.fromDate, dash.toDate);
//        //    return Ok(ApiResponseHelper.Success(result, "Data fetched successfully"));
//        //}

//        [HttpPost("GetDashBoardData")]
//        public async Task<ActionResult<DashBoard>> GetDashBoardData([FromBody] Dashboardget dash)
//        {
//            _logger.LogInformation("Received Dashboard Data request: EmployeeId: {EmployeeId}, PerDay: {PerDay}, FromDate: {FromDate}, ToDate: {ToDate}",
//                dash.employeeId, dash.perDay, dash.fromDate, dash.toDate);

//            var result = await _dashBoardDataRepo.GetDashBoardData(dash.employeeId, dash.perDay, dash.fromDate, dash.toDate);
//            if (result == null)
//            {
//                _logger.LogWarning("No data returned from dashboard service");
//                return NotFound("No data found.");
//            }

//            return Ok(ApiResponseHelper.Success(result, "Data fetched successfully"));
//        }
//    }

//    }




using APIGateWay.Business_Layer.Interface;
using APIGateWay.BusinessLayer.Helpers;
using APIGateWay.ModalLayer;
using APIGateWay.ModalLayer.GETData;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace WGNestAPIGateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DashBoardDataController : ControllerBase
    {
        private readonly IDashBoardDataRepo _dashBoardDataRepo;
        private readonly ILogger<DashBoardDataController> _logger;

        // Constructor with proper dependency injection for ILogger
        public DashBoardDataController(IDashBoardDataRepo dashBoardDataRepo, ILogger<DashBoardDataController> logger)
        {
            _dashBoardDataRepo = dashBoardDataRepo;
            _logger = logger; // Assign the logger
        }

        [HttpPost("GetDashBoardData")]
        public async Task<ActionResult<DashBoard>> GetDashBoardData([FromBody] Dashboardget dash)
        {
            // Log the incoming request for dashboard data
            _logger.LogInformation("Received Dashboard Data request: EmployeeId: {EmployeeId}, PerDay: {PerDay}, FromDate: {FromDate}, ToDate: {ToDate}",
                dash.employeeId, dash.perDay, dash.fromDate, dash.toDate);

            // Fetch data from the repository
            var result = await _dashBoardDataRepo.GetDashBoardData(dash.employeeId, dash.perDay, dash.fromDate, dash.toDate);

            // Check if result is null and log the warning
            if (result == null)
            {
                _logger.LogWarning("No data returned from dashboard service");
                return NotFound("No data found.");
            }

            // Log successful data retrieval
            _logger.LogInformation("Data successfully fetched from dashboard service.");

            // Return success response
            return Ok(ApiResponseHelper.Success(result, "Data fetched successfully"));
        }
    }
}