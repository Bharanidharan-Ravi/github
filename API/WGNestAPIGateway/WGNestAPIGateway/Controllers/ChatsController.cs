using APIGateWay.Business_Layer.Interface;
using APIGateWay.BusinessLayer.Helpers;
using APIGateWay.ModalLayer.ChatsModal.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace APIGateway.Controllers
{
    /// <summary>
    /// End-to-end encrypted 1:1 chat. Bodies are ciphertext only — see ChatRepo.
    /// New messages are also pushed over SignalR as the "ChatMessage" event.
    /// </summary>
    [ApiController]
    [Route("api/[Controller]")]
    public class ChatsController : ControllerBase
    {
        private readonly IChatRepo _chatRepo;

        public ChatsController(IChatRepo chatRepo)
        {
            _chatRepo = chatRepo;
        }

        // GET /api/Chats — conversations of the logged-in user, most recent first
        [HttpGet]
        public async Task<IActionResult> List()
        {
            var result = await _chatRepo.GetMyConversationsAsync();
            return Ok(ApiResponseHelper.Success(result, "NO"));
        }

        // POST /api/Chats/direct   Body: { "UserId": "..." } — returns the existing conversation or creates it
        [HttpPost("direct")]
        public async Task<IActionResult> OpenDirect([FromBody] OpenDirectConversationDto dto)
        {
            if (dto == null)
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Request body is required." });

            var result = await _chatRepo.OpenDirectConversationAsync(dto.UserId);
            return Ok(ApiResponseHelper.Success(result, "NO"));
        }

        // GET /api/Chats/{conversationId}/messages?deviceId={guid}&before={datetime}&take=50
        [HttpGet("{conversationId:guid}/messages")]
        public async Task<IActionResult> Messages(
            Guid conversationId,
            [FromQuery] Guid deviceId,
            [FromQuery] DateTime? before,
            [FromQuery] int take = 50)
        {
            var result = await _chatRepo.GetMessagesAsync(conversationId, deviceId, before, take);
            return Ok(ApiResponseHelper.Success(result, "NO"));
        }

        // POST /api/Chats/{conversationId}/messages   Body: SendMessageDto
        [HttpPost("{conversationId:guid}/messages")]
        public async Task<IActionResult> Send(Guid conversationId, [FromBody] SendMessageDto dto)
        {
            if (dto == null)
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Request body is required." });

            var result = await _chatRepo.SendMessageAsync(conversationId, dto);
            return Ok(ApiResponseHelper.Success(result, "NO"));
        }
    }
}
