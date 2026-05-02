using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.GETData
{
    public class GetRepoUserData
    {
        [Key]
        public Guid? Repo_Id { get; set; }
        public string? UserName { get; set; }
        public Guid? UserId { get; set; }
        public string? RepoKey { get; set; }
        public string? PhoneNumber { get; set; }
        public string? MailId { get; set; }
        public string? Status { get; set; }
        public string? WGUserName { get; set; }
        
    }
}
