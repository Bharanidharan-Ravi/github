using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace WGAPP.BusinessLayer.Helpers.ilog
{
    public interface IlogHelper
    {
        Task LogExceptionAsync(Exception ex);
        Task LogInvalidLoginAttempt(Exception ex);
        Task SavePostingData(string module, string action, string postingdata, string response);
    }
}
