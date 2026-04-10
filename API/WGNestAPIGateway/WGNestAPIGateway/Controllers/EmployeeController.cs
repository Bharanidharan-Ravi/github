using APIGateWay.Business_Layer.Interface;
using APIGateWay.BusinessLayer.Helpers;
using APIGateWay.ModalLayer.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace APIGateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmployeeController:ControllerBase
    {
        private readonly IEmployeeRepo _employeeRepo;
        public EmployeeController(IEmployeeRepo employeeRepo)
        {
            _employeeRepo = employeeRepo;

        }

        [HttpPut("update/{employeeId}")]
        public async Task<IActionResult>UpdateEmploee(
            Guid employeeId, [FromBody] RegisterRequestDto dto)
        {
            var res =await _employeeRepo.UpdateEmployeeAsync(employeeId, dto);
            return Ok(ApiResponseHelper.Success(res,"Employee update successfully."));
        }

        //[HttpPut("{id:guid}")]
        //public async Task<IActionResult> UpdateEmployee(
        //    Guid id, [FromBody] EmployeeMasterDto employeeMasterDto)
        //{
        //    if (employeeMasterDto == null)
        //        return BadRequest(new
        //        {
        //            Code = "VALIDATION_ERROR",
        //            ErrorMessage = "Request body is required."
        //        });
        //    var result = await _employeeRepo.UpdateEmployeeAsync(id, employeeMasterDto);
        //    return Ok(result);
        //}
            
    }
}
