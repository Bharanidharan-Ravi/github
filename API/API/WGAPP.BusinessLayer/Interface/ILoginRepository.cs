using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WGAPP.BusinessLayer.Interface
{
    public interface ILoginRepository
    {
        Task<string> GetUserinfo(string username, string password, string deviceInfo);
    }
}
