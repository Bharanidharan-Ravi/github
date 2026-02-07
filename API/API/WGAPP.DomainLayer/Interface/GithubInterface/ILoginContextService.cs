using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WGAPP.DomainLayer.Interface.GithubInterface
{
    public interface ILoginContextService
    {
        Guid userId { get; }
        string userName { get; }
        string databaseName { get; }
        string ClientId { get; }
        string Status { get; }
        string Role { get; }
        string JwtToken { get; }
        string RequestPath { get; }
    }
}

