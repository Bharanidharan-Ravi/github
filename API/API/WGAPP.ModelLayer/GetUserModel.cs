using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace WGAPP.ModelLayer
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
        public Guid UserId { get; set; }
        public string? UserName { get; set; }
        public string? ClientId { get; set; }
        public string? DBName { get; set; }
        public string? Status { get; set; }
        public string? Key { get; set; }
        public int? Role { get; set; }
    
    }
    public class UserInfo
    {
        public Guid UserId { get; set; }
        public string? UserName { get; set; }
        public string? ClientId { get; set; }
        public string? DBName { get; set; }
        public string? Status { get; set; }
        public string? Key { get; set; }
        public string? JwtToken { get; set; }
        public int? Role { get; set; }


    }

}
