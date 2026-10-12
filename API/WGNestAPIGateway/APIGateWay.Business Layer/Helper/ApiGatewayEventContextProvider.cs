using APIGateWay.BusinessLayer.Helper.Events.Interface;
using APIGateWay.DomainLayer.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.BusinessLayer.Helper
{
    public class ApiGatewayEventContextProvider
    : IEventContextProvider
    {
        private readonly ILoginContextService _loginContext;

        public ApiGatewayEventContextProvider(
            ILoginContextService loginContext)
        {
            _loginContext = loginContext;
        }

        public Dictionary<string, string> GetContextValues()
        {
            return new Dictionary<string, string>
        {
            {
                "UserId",
                _loginContext.userId.ToString()
            },

            {
                "UserName",
                _loginContext.userName ?? string.Empty
            },

            {
                "Role",
                _loginContext.role.ToString()
            }
        };
        }
    }
}
