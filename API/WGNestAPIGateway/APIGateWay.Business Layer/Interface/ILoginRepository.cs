using APIGateWay.ModalLayer.DTOs;
using APIGateWay.ModalLayer.GETData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.BusinessLayer.Interface
{
    public interface ILoginRepository
    {
        Task<GetUserList> RegisterUserAsync(RegisterRequestDto request);
        Task<string> GetUserinfo(string username, string password, string deviceInfo);
        //Task<List<GetEmployee>> GetEmployeeMaster();
    }
}
