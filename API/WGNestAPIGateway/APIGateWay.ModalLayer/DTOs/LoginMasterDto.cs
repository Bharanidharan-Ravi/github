using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.DTOs
{
    public class LoginMasterDto
    {
        public string UserName { get; set; }
        public string? Password { get; set; }
        public int? Role { get; set; }
        public string? DBName { get; set; }
        public string? Status { get; set; }
    }

}
