using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.DTOs
{
    public class PostCustomerDto
    {
        public Guid? Repo_Id { get; set; }
       
        public string CustomerName { get; set; }    
        public string? UserName { get; set; }
        public string? RepoKey { get; set; }
        public string? PhoneNumber { get; set; }
        public string? MailId { get; set; }
        public int? Role { get; set; }
        public string? Password{ get; set; }
    }

    public class PutCustomerdto
    {
        public Guid? Repo_Id { get; set; }
        public string CustomerName { get; set; }
        public Guid? UserId { get; set; }
        public string? NewCustomerName { get; set; }
        public string? RepoKey { get; set; }
        public string? PhoneNumber { get; set; }
        public string? MailId { get; set; }
       
        public string? Status { get; set; }

    }
}
