using APIGateWay.ModalLayer.DTOs;
using APIGateWay.ModalLayer.GETData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.BusinessLayer.Interface
{
    public interface ICustomerRepo
    {
        Task<GetCustomerDto> PostCustomer(PostCustomerDto dto);
        Task<GetCustomerDto> PutCustomer(Guid userId, PutCustomerdto dto);
    }
}
