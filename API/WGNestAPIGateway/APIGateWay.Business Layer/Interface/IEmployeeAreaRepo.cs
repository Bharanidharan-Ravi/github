using APIGateWay.ModalLayer.GETData;
using APIGateWay.ModalLayer.PostData;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Interface
{
    public interface IEmployeeAreaRepo
    {
        Task<GetHoliday> CreateHolidayAsync(PostHolidayDto dto);
        Task<GetHoliday> UpdateHolidayAsync(int id, PostHolidayDto dto);
        Task<PolicyDto> UploadPolicyAsync(PostPolicyDto dto);
    }
}
