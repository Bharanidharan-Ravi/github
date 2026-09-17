using APIGateWay.ModalLayer.ChatsModal.DTOs;

namespace APIGateWay.Business_Layer.Interface
{
    public interface IChatRepo
    {
        Task<List<ConversationDto>> GetMyConversationsAsync();
        Task<ConversationDto> OpenDirectConversationAsync(Guid otherUserId);
        Task<ConversationDto> OpenGroupConversationAsync(CreateGroupConversationDto dto);
        Task<ConversationDto> UpdateGroupMembersAsync(Guid conversationId, ManageGroupMembersDto dto);

        /// <summary>Oldest-first page of messages created before <paramref name="before"/>.</summary>
        Task<List<ChatMessageDto>> GetMessagesAsync(Guid conversationId, DateTime? before, int take);
        Task<ChatMessageDto> SendMessageAsync(Guid conversationId, SendMessageDto dto);
        Task<ChatMessageDto> SendMediaMessageAsync(Guid conversationId, SendMediaMessageDto dto);
        Task<ConversationReadDto> MarkReadAsync(Guid conversationId, MarkConversationReadDto dto);
        Task<MessageReactionEventDto> ToggleReactionAsync(Guid messageId, ToggleReactionDto dto);

        /// <summary>Base64 of the encrypted file's bytes, after checking the caller is a member of the
        /// conversation the media's message belongs to. 404 (DataNotFoundException) if missing or not a member.
        /// Returned as base64/JSON rather than a raw stream — this API's ResponseWrappingMiddleware buffers
        /// and re-encodes the response body as UTF-8 text, which would corrupt arbitrary binary bytes; the
        /// existing AttachmentController.Download follows the same base64-JSON convention for that reason.</summary>
        Task<string> GetMediaBase64Async(Guid mediaId);
    }
}
