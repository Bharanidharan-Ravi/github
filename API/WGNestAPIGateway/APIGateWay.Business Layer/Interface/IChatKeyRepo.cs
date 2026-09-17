using APIGateWay.ModalLayer.ChatsModal.DTOs;

namespace APIGateWay.Business_Layer.Interface
{
    public interface IChatKeyRepo
    {
        Task<ChatKeyStatusDto> GetStatusAsync(Guid deviceId);
        Task<ChatKeyStatusDto> RegisterIdentityKeyAsync(RegisterIdentityKeyDto dto);
        Task<ChatKeyStatusDto> RegisterSignedPreKeyAsync(RegisterSignedPreKeyDto dto);
        Task<List<ParticipantKeysDto>> GetParticipantKeysAsync(IReadOnlyCollection<Guid> userIds);
    }
}
