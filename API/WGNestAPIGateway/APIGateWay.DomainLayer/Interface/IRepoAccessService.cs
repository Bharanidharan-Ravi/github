using System;

namespace APIGateWay.DomainLayer.Interface
{
    public interface IRepoAccessService
    {
        Task<List<string>> GetUserRepoIdsAsync(Guid userId);
    }
}
