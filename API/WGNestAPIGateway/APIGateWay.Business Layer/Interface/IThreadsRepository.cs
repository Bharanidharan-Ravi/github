using APIGateWay.ModalLayer.GETData;
using APIGateWay.ModalLayer.PostData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.BusinessLayer.Interface
{
    public interface IThreadsRepository
    {
        Task<ThreadList> CreateThreadAsync(PostThreadsDto threadDto);
        Task<ThreadList> UpdateThreadAsync(long threadId, UpdateThreadDto dto);
    }
}
