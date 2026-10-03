using APIGateWay.Business_Layer.Interface;
using APIGateWay.BusinessLayer.Helpers;
using APIGateWay.ModalLayer.PostData;
using Microsoft.AspNetCore.Mvc;

namespace APIGateway.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EmployeeAreaController : ControllerBase
    {
        private readonly IEmployeeAreaRepo _repo;

        public EmployeeAreaController(IEmployeeAreaRepo repo)
        {
            _repo = repo;  
        }

        [HttpPost("holiday")]
        public async Task<IActionResult> CreateHoliday([FromBody] PostHolidayDto dto)
        {
            var res = await _repo.CreateHolidayAsync(dto);
            return Ok(ApiResponseHelper.Success(res));
        }

        [HttpPut("holiday/{id}")]
        public async Task<IActionResult> UpdateHoliday(int id, [FromBody] PostHolidayDto dto)
        {
            var res = await _repo.UpdateHolidayAsync(id,dto);
            return Ok(ApiResponseHelper.Success(res));
        }


        [HttpPost("policy/upload")]
        public async Task<IActionResult> UploadPolicy([FromBody] PostPolicyDto dto)
        {
            if (dto?.temp?.temps == null || !dto.temp.temps.Any())
                return BadRequest(ApiResponseHelper.Failure("No file payload provided."));

            var res = await _repo.UploadPolicyAsync(dto);
            return Ok(ApiResponseHelper.Success(res));
        }
    }
}
