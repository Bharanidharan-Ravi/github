using APIGateWay.ModalLayer.DTOs;
using APIGateWay.ModalLayer.GETData;
using System;

namespace APIGateWay.BusinessLayer.Interface
{
    public interface ILoginRepository
    {
        Task<GetUserList> RegisterUserAsync(RegisterRequestDto request);
        Task<string> GetUserinfo(string username, string password, string deviceInfo);
        //Task<List<GetEmployee>> GetEmployeeMaster();
    }
}
