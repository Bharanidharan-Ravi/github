using APIGateWay.Business_Layer.Interface;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.DTOs;
using APIGateWay.ModalLayer.GETData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Repository
{
    public class CustomerRepo : ICustomerRepo
    {
        private readonly ICustomersService _customerService;
        private readonly ILoginContextService _loginContextService;

        public CustomerRepo(
            ICustomersService customersService,
            ILoginContextService loginContextService)

        {
            _customerService = customersService;
            _loginContextService = loginContextService;
        }
        public async Task<GetCustomerDto> PostCustomer(PostCustomerDto dto)
        {
            var res = await _customerService.PostCustomer(dto, _loginContextService.databaseName);
            return res;
        }

        public async Task<GetCustomerDto> PutCustomer(Guid userId, PutCustomerdto dto)
        {
            var res = await _customerService.PutCustomer(userId,dto, _loginContextService.databaseName);
            return res;
        }
    }
}
