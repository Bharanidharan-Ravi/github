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
        //Task<List<GetEmployee>> GetEmployeeMaster();
    }
}
