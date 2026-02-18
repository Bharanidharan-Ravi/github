using APIGateWay.ModalLayer.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.BusinessLayer.Interface
{
    public interface IRepoRepository
    {
        Task<string> PostRepo(PostRepoDto repo);
    }
}
