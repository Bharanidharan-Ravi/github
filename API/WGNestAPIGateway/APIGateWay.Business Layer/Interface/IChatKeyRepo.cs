using APIGateWay.ModalLayer.ChatsModal.DTOs;

namespace APIGateWay.Business_Layer.Interface
{
    public interface IChatKeyRepo
    {
        /// <summary>Null when the logged-in user has not registered a chat key yet.</summary>
        Task<ChatUserKeyBundleDto?> GetMyKeyBundleAsync();
        Task<ChatUserKeyBundleDto> RegisterMyKeyAsync(RegisterChatUserKeyDto dto);
        Task<ChatUserKeyBundleDto> RewrapMyKeyAsync(RewrapChatUserKeyDto dto);
        Task<List<ParticipantPublicKeyDto>> GetParticipantKeysAsync(IReadOnlyCollection<Guid> userIds);
    }
}
