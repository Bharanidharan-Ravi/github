using APIGateWay.Business_Layer.Interface;
using APIGateWay.BusinessLayer.Helpers;
using APIGateWay.ModalLayer.PostData;
using Microsoft.AspNetCore.Mvc;

namespace APIGateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CurrentHolderController : ControllerBase
    {
        private readonly ICurrentHolderRepo _currentHolderRepo;
        public CurrentHolderController(ICurrentHolderRepo currentHolderRepo)
        {
            _currentHolderRepo = currentHolderRepo;
        }
        [HttpPut("{issueId:guid}")]
        public async Task<IActionResult> UpdateCurrentHolder(Guid issueId, [FromBody] postAssigneeToMoveDto dto)
        {
            if (dto == null)
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Request body is required." });
            var result = await _currentHolderRepo.UpdateCurrentHolderAsync(issueId, dto);
            return Ok(ApiResponseHelper.Success(result, "Move To Updated Successfully."));

        }
    }
}
