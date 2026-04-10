using APIGateWay.ModalLayer.PostData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.DTOs
{
    public class RegisterRequestDto
    {
        public string CreatedFor { get; set; } // 👈 "Client" or "Employee"

        public LoginMasterDto Login { get; set; }

        // Optional — only one will be used based on CreatedFor
        public ClientMasterDto? Client { get; set; }
        public EmployeeMasterDto? Employee { get; set; }

        public TempReturn? temp { get; set; }
    }

}
