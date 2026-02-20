using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static APIGateWay.ModalLayer.PostData.PostHelper;

namespace APIGateWay.ModalLayer.MasterData
{
    public class ProjectMaster : IAuditableEntity, IAuditableUser
    {
        public int SiNo { get; set; }
        public string ProjectKey { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string RepoKey { get; set; }
        public Guid? Responsible { get; set; }
        public string? Status { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? CreatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public Guid? UpdatedBy { get; set; }
        public Guid? Id { get; set; }
        public Guid? Repo_Id { get; set; }
        public string? HtmlDesc { get; set; }
    }
}
