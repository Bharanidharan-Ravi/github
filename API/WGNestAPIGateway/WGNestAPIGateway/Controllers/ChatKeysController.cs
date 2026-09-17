using APIGateWay.Business_Layer.Interface;
using APIGateWay.BusinessLayer.Helpers;
using APIGateWay.ModalLayer.ChatsModal.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace APIGateway.Controllers
{
    /// <summary>
    /// Public key directory for end-to-end encrypted chat.
    /// Called by the UI after login and whenever the weekly pre-key is due.
    /// Responses use message "NO" so the UI does not show a success toast.
    /// </summary>
    [ApiController]
    [Route("api/[Controller]")]
    public class ChatKeysController : ControllerBase
    {
        private readonly IChatKeyRepo _chatKeyRepo;

        public ChatKeysController(IChatKeyRepo chatKeyRepo)
        {
            _chatKeyRepo = chatKeyRepo;
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /api/ChatKeys/status?deviceId={guid}
        // What the server holds for the logged-in user on this device.
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet("status")]
        public async Task<IActionResult> Status([FromQuery] Guid deviceId)
        {
            var result = await _chatKeyRepo.GetStatusAsync(deviceId);
            return Ok(ApiResponseHelper.Success(result, "NO"));
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /api/ChatKeys/identity
        // Body: { "DeviceId": "...", "PublicKey": "<base64 SPKI>", "DeviceInfo": "..." }
        // ─────────────────────────────────────────────────────────────────────
        [HttpPost("identity")]
        public async Task<IActionResult> RegisterIdentity([FromBody] RegisterIdentityKeyDto dto)
        {
            if (dto == null)
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Request body is required." });

            var result = await _chatKeyRepo.RegisterIdentityKeyAsync(dto);
            return Ok(ApiResponseHelper.Success(result, "NO"));
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /api/ChatKeys/prekey
        // Body: { "DeviceId": "...", "KeyId": 1, "PublicKey": "<base64 SPKI>", "Signature": "<base64>" }
        // Replaces the active pre-key; the new one expires in 7 days.
        // ─────────────────────────────────────────────────────────────────────
        [HttpPost("prekey")]
        public async Task<IActionResult> RegisterPreKey([FromBody] RegisterSignedPreKeyDto dto)
        {
            if (dto == null)
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Request body is required." });

            var result = await _chatKeyRepo.RegisterSignedPreKeyAsync(dto);
            return Ok(ApiResponseHelper.Success(result, "NO"));
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /api/ChatKeys/participants?userIds={guid}&userIds={guid}...
        // Every usable device (active identity + valid pre-key) for each requested
        // user, so the sender can encrypt a message to all of them before any chat
        // message exists. Users with no usable device yet come back with an empty
        // Devices list.
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet("participants")]
        public async Task<IActionResult> Participants([FromQuery] List<Guid> userIds)
        {
            var result = await _chatKeyRepo.GetParticipantKeysAsync(userIds);
            return Ok(ApiResponseHelper.Success(result, "NO"));
        }
    }
}
