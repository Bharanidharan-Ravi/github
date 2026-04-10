using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.DTOs
{
    public class EmployeeMasterDto
    {
        public string EmployeeName { get; set; }
        public int? Team { get; set; }
        public int? Role { get; set; }
        public string? Specialization { get; set; }
        public DateOnly? DoB { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Status { get; set; }
    }
}
