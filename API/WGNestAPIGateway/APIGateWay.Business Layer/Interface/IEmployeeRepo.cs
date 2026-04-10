using APIGateWay.ModalLayer.DTOs;
using APIGateWay.ModalLayer.GETData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Interface
{
    public interface IEmployeeRepo
    {
        Task<GetEmployee> UpdateEmployeeAsync(Guid employeeId, RegisterRequestDto dto);
    }
}
