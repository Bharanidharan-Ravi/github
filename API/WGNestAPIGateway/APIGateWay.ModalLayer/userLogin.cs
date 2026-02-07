using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer
{
    public class userLogin
    {
        public string? UserName { get; set; }
        public string? Password { get; set; }
        public string? DeviceInfo { get; set; }
    }
    public class GetUserModel
    {
        [Key]
        public int UserId { get; set; }
        public string? UserName { get; set; }
        public string? DBName { get; set; }
        public string? Status { get; set; }
        public string? Role { get; set; }
        public string? PasswordHash { get; set; }
        public string? PasswordSalt { get; set; }

    }
}
