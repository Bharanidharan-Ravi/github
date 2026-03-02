using APIGateWay.Business_Layer.Interface;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.DomainLayer.Service;
using APIGateWay.ModalLayer.GETData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Repository
{
    public class DashBoardDataRepo : IDashBoardDataRepo
    {
        private readonly IDashBoardDataService _dashboardDataService;
        public DashBoardDataRepo (IDashBoardDataService dashboardDataService)
        {
            _dashboardDataService = dashboardDataService;
        }
        public async Task<DashBoard> GetDashBoardData(Guid? employeeId = null, DateTime? perDay = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            return await _dashboardDataService.GetDashBoardData(employeeId, perDay, fromDate, toDate);
        }
        
    }
}
