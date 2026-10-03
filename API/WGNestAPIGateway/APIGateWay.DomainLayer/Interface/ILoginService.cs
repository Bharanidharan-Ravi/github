using APIGateWay.ModalLayer.DTOs;
using APIGateWay.ModalLayer.GETData;
using System;

namespace APIGateWay.DomainLayer.Interface
{
    public interface ILoginService
    {
        Task<GetUserList> RegisterUserAsync(RegisterRequestDto request);
        Task<List<GetUserforValidate>> GetUser(string username, string password, string deviceInfo);
        (string hash, string salt) HashPasswordAgron(string password);
        Task<List<int>> GetEffectiveRolesAsync(Guid userId, int? loginRole);
        //Task<List<GetEmployee>> GetEmployeeMaster();
    }
}
