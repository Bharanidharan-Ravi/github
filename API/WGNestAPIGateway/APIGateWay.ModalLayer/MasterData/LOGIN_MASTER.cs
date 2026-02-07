using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.MasterData
{
    [Table("WGUserDetails")]
    public class LOGIN_MASTER
    {
        [Key]
        public Guid UserID { get; set; }
        public string UserName { get; set; }
        public string? PasswordHash { get; set; }
        public string? Salt { get; set; }
        public string? Password { get; set; }
        public string DBName { get; set; }
        public Guid? ClientId { get; set; }
        public string? Status { get; set; }
        public string? Key { get; set; }
        public int Role { get; set; }
    }
}
