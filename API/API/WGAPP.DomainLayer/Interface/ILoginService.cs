using WGAPP.ModelLayer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WGAPP.DomainLayer.Interface
{
    public interface ILoginService
    {
        Task<List<GetUserModel>> GetUser(string username, string password, string deviceInfo);
    }
}
