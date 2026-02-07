using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.GETData
{
    public class GetUserList
    {
        [Key]
        public Guid UserID { get; set; }
        public string Username { get; set; }
        public string Status { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }
}
