using APIGateWay.ModalLayer.GETData;
using APIGateWay.ModalLayer.PostData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Interface
{
    public interface IDailyPlanRepo
    {
        Task<List<GetDailyPlan>> GetTodayPlanAsync(DateTime date);
        Task<List<GetDailyPlan>> CheckTicketAsync(List<CreateDailyPlanDto> dto);
        Task<GetDailyPlan> UncheckTicketAsync(int planId, UncheckPlanDto dto);
    }
}
