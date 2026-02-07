using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.DomainLayer.Interface
{
    public interface ILoginContextService
    {
        Guid userId { get; }
        string userName { get; }
        string databaseName { get; }
        string Status { get; }
        string Role { get; }
        string JwtToken { get; }
        string RequestPath { get; }
    }
}
