using APIGateWay.ModalLayer.DTOs;
using APIGateWay.ModalLayer.GETData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.DomainLayer.Interface
{
    public interface ILoginService
    {
        Task<GetUserList> RegisterUserAsync(RegisterRequestDto request);
        Task<List<GetUserforValidate>> GetUser(string username, string password, string deviceInfo);
        (string hash, string salt) HashPasswordAgron(string password);
        Task<List<GetEmployee>> GetEmployeeMaster();
    }
}
