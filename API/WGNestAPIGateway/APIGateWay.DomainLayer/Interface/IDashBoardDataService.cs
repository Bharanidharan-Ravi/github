using APIGateWay.ModalLayer.GETData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.DomainLayer.Interface
{
    public interface IDashBoardDataService
    {
        Task<DashBoard> GetDashBoardData(Guid? employeeId = null, DateTime? perDay = null, DateTime? fromDate = null, DateTime? toDate = null);
    }
}  
