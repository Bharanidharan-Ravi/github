using APIGateWay.Business_Layer.Interface;
using APIGateWay.Business_Layer.Repository;
using APIGateWay.BusinessLayer.Helpers;
using APIGateWay.ModalLayer.GETData;
using APIGateWay.ModalLayer.PostData;
using Microsoft.AspNetCore.Mvc;
using ReverseMarkdown.Converters;

namespace APIGateway.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BannermessageController :ControllerBase
    {
        private readonly IBannermessageRepo _repo;
        public BannermessageController(IBannermessageRepo repo)
        {
            _repo = repo;
        }
        [HttpPost("CreateBannerMessage")]
        public async Task<IActionResult> CreateBannerMessage([FromBody]PostBannerMessageDto dto)
        {
            if (dto == null)
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Requested body is required." });
            if (string.IsNullOrWhiteSpace(dto.MessageText))
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "MessageText is required." });
            if (dto.MessageTypeId == Guid.Empty)
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Message Type is required." });
            var response = await _repo.GetBannerMessageAsync(dto);
            return Ok(ApiResponseHelper.Success(response, "Banner message created successfully."));
        }
        [HttpPut("UpdateBannerMessage/{BannerMessageId:guid}")]
        public async Task<IActionResult> UpdateBannerMessage(Guid BannerMessageId, [FromBody]PutBannerMessageDto dto)
        {
         
          if(string.IsNullOrWhiteSpace(dto.MessageText))
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Message Id is required." });
            if (dto.MessageTypeId == Guid.Empty)
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Message Type is required." });
            var response = await _repo.UpdateBannerMessageAsync(BannerMessageId,dto);
            return Ok(ApiResponseHelper.Success(response, "Banner message updated successfully."));
        }
        [HttpGet("GetBannerMessage")]
        public async Task<IActionResult> GetBannerMessages()
        {
            var response = await _repo.GetBannerMessagesAsync();
            return Ok(ApiResponseHelper.Success(response, "Banner message updated successfully."));
        }
    }
}
