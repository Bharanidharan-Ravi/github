using System;

namespace APIGateWay.BusinessLayer.Helpers.ilog
{
    public interface IlogHelper
    {
        Task LogExceptionAsync(Exception ex);
        Task LogInvalidLoginAttempt(Exception ex);
        Task SavePostingData(string module, string action, string postingdata, string response);
    }
}
