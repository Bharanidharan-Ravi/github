using APIGateWay.ModalLayer.GETData;
using APIGateWay.ModalLayer.PostData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Interface
{
    public interface ILabelRepo
    {
        Task<GetLabel> CreateLabelAsync(CreateLabelDto dto);
        Task<GetLabel> UpdateLabelAsync(int id, UpdateLabelDto dto);
        Task<GetLabel> UpdateLabelStatusAsync(int id, UpdateLabelStatusDto dto);
        //Task DeleteLabelAsync(int id);
    }

}
