using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static APIGateWay.ModalLayer.Helper.PostHelper;

namespace APIGateWay.ModalLayer.PostData
{
    public class IssuesPost : IAuditableEntity, IAuditableUser
    {
        [Key]
        public Guid? Issue_Id { get; set; }
        public int? SiNo { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public Guid? RepoId { get; set; }
        public string? RepoKey { get; set; }
        public Guid? ProjectId { get; set; }
        public Guid? Assignee_Id { get; set; }
        public DateTime? Due_Date { get; set; }
        public DateTime From_Time { get; set; }
        public DateTime To_Time { get; set; }
        public string Hours { get; set; }
        public string? Status { get; set; }
        public Guid? Issuelink_Id { get; set; }
        public string? Issue_Code { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTime? CreatedAt { get; set; }
        public Guid? UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        // Navigation
        // public virtual ICollection<IssueAttachment> IssueAttachments { get; set; }
    }

    public class ISSUE_LABELS
    {
        [Key]
        public int Label_Id { get; set; }
        public Guid? Issue_Id { get; set; }
    }



    public class IssueMasterDto
    {
        public Guid? Repo_Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string RepoKey { get; set; }
        public Guid? Issuer_Id { get; set; }
        public DateTime Created_On { get; set; }
        public DateTime? Updated_On { get; set; }
        public Guid? Project_Id { get; set; }
        public Guid? Assignee_Id { get; set; }
        public DateTime Due_Date { get; set; }
        public string? Status { get; set; }
        public Guid Issuelink_Id { get; set; }
        public string Issue_Code { get; set; }
        public Guid? Updated_By { get; set; }
        public string? Hours { get; set; }
        public List<ISSUE_LABELS> Labels { get; set; }
        public TempReturn TempReturns { get; set; }
    }

}
