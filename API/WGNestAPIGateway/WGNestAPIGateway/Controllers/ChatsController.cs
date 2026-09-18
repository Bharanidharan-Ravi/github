using APIGateWay.Business_Layer.Interface;
using APIGateWay.BusinessLayer.Helpers;
using APIGateWay.ModalLayer.ChatsModal.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace APIGateway.Controllers
{
    /// <summary>
    /// End-to-end encrypted conversations. Bodies carry ciphertext only — see ChatRepo.
    /// New messages are also pushed over SignalR ("ChatMessage"), read markers as "ChatRead".
    /// Responses use message "NO" so the UI does not show a success toast.
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

        // ─────────────────────────────────────────────────────────────────────
        // GET /api/Chats
        // The caller's conversations, most recent first, with unread counts
        // and the newest message (ciphertext + the caller's wrapped key).
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> List()
        {
            var result = await _chatRepo.GetMyConversationsAsync();
            return Ok(ApiResponseHelper.Success(result, "NO"));
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /api/Chats/direct   Body: { UserId }
        // Returns the existing 1:1 conversation with that user, or creates it.
        // ─────────────────────────────────────────────────────────────────────
        [HttpPost("direct")]
        public async Task<IActionResult> OpenDirect([FromBody] OpenDirectConversationDto dto)
        {
            if (dto == null)
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Request body is required." });

            var result = await _chatRepo.OpenDirectConversationAsync(dto.UserId);
            return Ok(ApiResponseHelper.Success(result, "NO"));
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /api/Chats/group   Body: { Title, MemberUserIds }
        // Creates a group conversation; the caller becomes its Admin.
        // ─────────────────────────────────────────────────────────────────────
        [HttpPost("group")]
        public async Task<IActionResult> OpenGroup([FromBody] CreateGroupConversationDto dto)
        {
            if (dto == null)
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Request body is required." });

            var result = await _chatRepo.OpenGroupConversationAsync(dto);
            return Ok(ApiResponseHelper.Success(result, "NO"));
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /api/Chats/{conversationId}/members   Body: { AddUserIds, RemoveUserIds }
        // Admin only. Returns the conversation with its updated member list.
        // ─────────────────────────────────────────────────────────────────────
        [HttpPost("{conversationId:guid}/members")]
        public async Task<IActionResult> UpdateMembers(Guid conversationId, [FromBody] ManageGroupMembersDto dto)
        {
            if (dto == null)
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Request body is required." });

            var result = await _chatRepo.UpdateGroupMembersAsync(conversationId, dto);
            return Ok(ApiResponseHelper.Success(result, "NO"));
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /api/Chats/{conversationId}/icon   multipart/form-data: Icon
        // Admin only. Group icons aren't end-to-end encrypted — a plain static file,
        // same trust level as an employee profile photo.
        // ─────────────────────────────────────────────────────────────────────
        [HttpPost("{conversationId:guid}/icon")]
        [RequestSizeLimit(5_242_880)] // 5 MB
        [RequestFormLimits(MultipartBodyLengthLimit = 5_242_880)]
        public async Task<IActionResult> UpdateIcon(Guid conversationId, [FromForm] UploadGroupIconDto dto)
        {
            if (dto == null)
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Request body is required." });

            var result = await _chatRepo.UpdateGroupIconAsync(conversationId, dto.Icon);
            return Ok(ApiResponseHelper.Success(result, "NO"));
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /api/Chats/{conversationId}/messages?before={datetime}&take=50
        // Oldest-first page; each message carries only the caller's wrapped key.
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet("{conversationId:guid}/messages")]
        public async Task<IActionResult> Messages(
            Guid conversationId,
            [FromQuery] DateTime? before,
            [FromQuery] int take = 50)
        {
            var result = await _chatRepo.GetMessagesAsync(conversationId, before, take);
            return Ok(ApiResponseHelper.Success(result, "NO"));
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /api/Chats/{conversationId}/messages
        // Body: { ClientMessageId, EncryptedPayload, Keys: [{ RecipientUserId, WrappedMessageKey }] }
        // Idempotent on ClientMessageId.
        // ─────────────────────────────────────────────────────────────────────
        [HttpPost("{conversationId:guid}/messages")]
        public async Task<IActionResult> Send(Guid conversationId, [FromBody] SendMessageDto dto)
        {
            if (dto == null)
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Request body is required." });

            var result = await _chatRepo.SendMessageAsync(conversationId, dto);
            return Ok(ApiResponseHelper.Success(result, "NO"));
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /api/Chats/{conversationId}/media   multipart/form-data
        // Same envelope fields as a text send, plus the encrypted file. The file's
        // key is wrapped with the message's own content key, never sent unwrapped.
        // ─────────────────────────────────────────────────────────────────────
        [HttpPost("{conversationId:guid}/media")]
        [RequestSizeLimit(52_428_800)] // 50 MB
        [RequestFormLimits(MultipartBodyLengthLimit = 52_428_800)]
        public async Task<IActionResult> SendMedia(Guid conversationId, [FromForm] SendMediaMessageDto dto)
        {
            if (dto == null)
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Request body is required." });

            var result = await _chatRepo.SendMediaMessageAsync(conversationId, dto);
            return Ok(ApiResponseHelper.Success(result, "NO"));
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /api/Chats/media/{mediaId}
        // Returns the encrypted file's bytes (ciphertext only) as base64 to authorized
        // conversation members; decryption happens entirely client-side. Base64/JSON
        // rather than a raw stream — see IChatRepo.GetMediaBase64Async's doc comment.
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet("media/{mediaId:guid}")]
        public async Task<IActionResult> DownloadMedia(Guid mediaId)
        {
            var fileData = await _chatRepo.GetMediaBase64Async(mediaId);
            return Ok(ApiResponseHelper.Success(new { FileData = fileData }, "NO"));
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /api/Chats/{conversationId}/read   Body: { ReadUpTo }
        // Moves the caller's read marker forward (never back).
        // ─────────────────────────────────────────────────────────────────────
        [HttpPost("{conversationId:guid}/read")]
        public async Task<IActionResult> MarkRead(Guid conversationId, [FromBody] MarkConversationReadDto? dto)
        {
            var result = await _chatRepo.MarkReadAsync(conversationId, dto ?? new MarkConversationReadDto());
            return Ok(ApiResponseHelper.Success(result, "NO"));
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /api/Chats/messages/{messageId}/reactions   Body: { Emoji }
        // Toggles the caller's reaction (adds if missing, removes if already set).
        // Also pushed over SignalR as "MessageReactionChanged" to every member.
        // ─────────────────────────────────────────────────────────────────────
        [HttpPost("messages/{messageId:guid}/reactions")]
        public async Task<IActionResult> ToggleReaction(Guid messageId, [FromBody] ToggleReactionDto dto)
        {
            if (dto == null)
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Request body is required." });

            var result = await _chatRepo.ToggleReactionAsync(messageId, dto);
            return Ok(ApiResponseHelper.Success(result, "NO"));
        }
    }
}
