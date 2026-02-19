using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.DomainLayer.Interface
{
    public interface IRepoAccessService
    {
        Task<List<string>> GetUserRepoIdsAsync(Guid userId);
    }
}
