using APIGateWay.ModalLayer.DTOs;
using APIGateWay.ModalLayer.GETData;
using System;

namespace APIGateWay.DomainLayer.Interface
{
    public interface IRepoService
    {
        Task<GetRepo> PostRepo(PostRepoDto repo);
    }
}
