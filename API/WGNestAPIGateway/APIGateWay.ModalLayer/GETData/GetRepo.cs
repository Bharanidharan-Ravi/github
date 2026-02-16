using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.GETData
{
    public class GetRepo
    {
        [Key]
        public Guid Repo_Id { get; set; }
        public string? RepoKey { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public DateTime? CreatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public string? OwnerName { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public Guid? UpdatedBy { get; set; }
        public string? Status { get; set; }

        public string? RepoUserList { get; set; }
        public List<RepoUser> repoUsers =>
            string.IsNullOrEmpty(RepoUserList)
                ? new List<RepoUser>()
                : JsonConvert.DeserializeObject<List<RepoUser>>(RepoUserList);
    }

    public class RepoUser
    {
        [Key]
        public string UserName { get; set; }
        public string PhoneNumber { get; set; }
        public string MailId { get; set; }
        public string Status { get; set; }
    }
}
