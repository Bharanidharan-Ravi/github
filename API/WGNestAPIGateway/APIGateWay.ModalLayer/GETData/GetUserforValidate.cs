using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.GETData
{
    public class GetUserforValidate
    {
        [Key]
        public Guid UserId { get; set; }
        public string? UserName { get; set; }
        public string? DBName { get; set; }
        public string? Status { get; set; }
        public int? Role { get; set; }
        public string? PasswordHash { get; set; }
        public string? Salt { get; set; }
    }
}
