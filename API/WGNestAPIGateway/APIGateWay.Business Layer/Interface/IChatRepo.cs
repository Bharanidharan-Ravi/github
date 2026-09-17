using APIGateWay.ModalLayer.ChatsModal.DTOs;

namespace APIGateWay.Business_Layer.Interface
{
    public interface IChatRepo
    {
        Task<ConversationDto> OpenDirectConversationAsync(Guid otherUserId);
        Task<List<ConversationDto>> GetMyConversationsAsync();
        Task<List<ChatMessageDto>> GetMessagesAsync(Guid conversationId, Guid deviceId, DateTime? before, int take);
        Task<ChatMessageDto> SendMessageAsync(Guid conversationId, SendMessageDto dto);
    }
}
