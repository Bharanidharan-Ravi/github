using APIGateWay.ModalLayer.DTOs;
using System;

namespace APIGateWay.BusinessLayer.Interface
{
    public interface IRepoRepository
    {
        Task<string> PostRepo(PostRepoDto repo);
    }
}
