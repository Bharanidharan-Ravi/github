using APIGateWay.ModalLayer.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.DomainLayer.Interface
{
    public interface IRepoService
    {
        Task<PostRepositoryModel> PostRepo(LoginMasterDto login, ClientMasterDto clientMaster, PostRepositoryModel repo);
    }
}
