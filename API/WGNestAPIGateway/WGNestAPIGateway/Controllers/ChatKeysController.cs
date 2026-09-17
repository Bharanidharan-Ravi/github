using APIGateWay.Business_Layer.Interface;
using APIGateWay.BusinessLayer.Helpers;
using APIGateWay.ModalLayer.ChatsModal.DTOs;
using APIGateWay.ModelLayer.ErrorException;
using Microsoft.AspNetCore.Mvc;

namespace APIGateway.Controllers
{
    /// <summary>
    /// User-based key directory for end-to-end encrypted chat.
    /// Responses use message "NO" so the UI does not show a success toast.
    /// Binary fields (wrapped keys, salts) are base64 strings.
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
        // GET /api/ChatKeys/me
        // The logged-in user's key bundle. 404 = no key registered yet.
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet("me")]
        public async Task<IActionResult> GetMine()
        {
            var result = await _chatKeyRepo.GetMyKeyBundleAsync()
                ?? throw new Exceptionlist.DataNotFoundException("No chat key is registered for this user.");
            return Ok(ApiResponseHelper.Success(result, "NO"));
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /api/ChatKeys/me
        // Body: { PublicKey, WrappedByPassword, PasswordSalt, WrappedByRecovery, RecoverySalt }
        // First-time registration. Re-sending the same PublicKey is a no-op;
        // a different key when one already exists returns 409.
        // ─────────────────────────────────────────────────────────────────────
        [HttpPost("me")]
        public async Task<IActionResult> RegisterMine([FromBody] RegisterChatUserKeyDto dto)
        {
            if (dto == null)
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Request body is required." });

            var result = await _chatKeyRepo.RegisterMyKeyAsync(dto);
            return Ok(ApiResponseHelper.Success(result, "NO"));
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /api/ChatKeys/rewrap
        // Body: { WrappedByPassword, PasswordSalt, KeyVersion }
        // After a password reset the client unlocks with the recovery code and
        // re-wraps the private key under the new password.
        // ─────────────────────────────────────────────────────────────────────
        [HttpPost("rewrap")]
        public async Task<IActionResult> Rewrap([FromBody] RewrapChatUserKeyDto dto)
        {
            if (dto == null)
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Request body is required." });

            var result = await _chatKeyRepo.RewrapMyKeyAsync(dto);
            return Ok(ApiResponseHelper.Success(result, "NO"));
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /api/ChatKeys/participants?userIds={guid}&userIds={guid}...
        // Public keys of the requested users. Users without a registered key
        // are omitted — the client treats them as "cannot encrypt to yet".
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet("participants")]
        public async Task<IActionResult> Participants([FromQuery] List<Guid> userIds)
        {
            var result = await _chatKeyRepo.GetParticipantKeysAsync(userIds);
            return Ok(ApiResponseHelper.Success(result, "NO"));
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /api/ChatKeys/{userId}/recovery-escrow
        // Admin-only. Decrypts the recovery code escrowed at registration so
        // support can re-issue it to a user who lost theirs.
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet("{userId:guid}/recovery-escrow")]
        public async Task<IActionResult> RecoveryEscrow(Guid userId)
        {
            var result = await _chatKeyRepo.GetRecoveryEscrowAsync(userId);
            return Ok(ApiResponseHelper.Success(result, "NO"));
        }
    }
}
