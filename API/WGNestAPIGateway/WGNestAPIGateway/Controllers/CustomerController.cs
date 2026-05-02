using APIGateWay.Business_Layer.Interface;
using APIGateWay.BusinessLayer.Helpers;
using APIGateWay.ModalLayer.DTOs;
using Microsoft.AspNetCore.Mvc;
using ReverseMarkdown.Converters;

namespace APIGateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CustomerController : ControllerBase
    {
        private readonly ICustomerRepo _customer;
        public CustomerController(ICustomerRepo customer)
        {
            _customer = customer;
        }
        [HttpPost("PostCustomer")]
        public async Task<IActionResult> PostCustomer([FromBody]PostCustomerDto dto)
        {
            var res=await _customer.PostCustomer(dto);
            return Ok(ApiResponseHelper.Success(res,"Cusetomer created successfully"));

        }

        [HttpPut("PutCostomer/{userId}")]
        public async Task<IActionResult> PutCustomer(Guid userId, [FromBody]PutCustomerdto dto)
        {
            var response = await _customer.PutCustomer(userId,dto);
            return Ok(ApiResponseHelper.Success(response,"Customer updated successfully."));
        }
    }
}
