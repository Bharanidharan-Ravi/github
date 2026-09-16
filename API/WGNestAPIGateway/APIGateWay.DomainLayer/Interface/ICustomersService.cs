using APIGateWay.ModalLayer.DTOs;
using APIGateWay.ModalLayer.GETData;
using APIGateWay.ModalLayer.PostData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.DomainLayer.Interface
{
    public interface ICustomersService
    {
        Task<GetCustomerDto> PostCustomer(PostCustomerDto dto);
        Task<GetCustomerDto> PutCustomer(Guid userId, PutCustomerdto dto);
    }
}
