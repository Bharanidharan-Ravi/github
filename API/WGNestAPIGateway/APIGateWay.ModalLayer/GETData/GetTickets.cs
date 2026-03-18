using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.GETData
{
    public class GetTickets
    {
        [Key]
        public Guid Issue_Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? HtmlDesc { get; set; }
        public Guid? CreatedBy { get; set; }
        public string? Issuer_Name { get; set; }
        public DateTime? CreatedAt { get; set; }
        public Guid? UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public Guid Project_Id { get; set; }
        public Guid? RepoId { get; set; }
        public decimal? CompletionPct { get; set; }
        public string? RepoKey { get; set; }
        public Guid? Assignee_Id { get; set; }
        public string? Assignee_Name { get; set; }
        public string? All_Assignees { get; set; }
        public string? Priority { get; set; }
        public DateTime? Due_Date { get; set; }
        public string? Status { get; set; }
        public string? Issue_Code { get; set; }
        public string? Hours { get; set; }
        public string? Labels_JSON { get; set; }
        public string? Attachment_JSON { get; set; }
        //public List<GetLabelForIssues> Labels_JSON { get; set; }
        //public List<GetAttachForIssues> Attachment_JSON { get; set; }
    }


    public class GetLabelForIssues
    {
        [Key]
        public int LABEL_ID { get; set; }
        public string Label_Title { get; set; }
        public string Label_COLOR { get; set; }
    }

    public class GetAttachForIssues
    {
        [Key]
        public int AttachmentId { get; set; }
        public string FileName { get; set; }
        public string PublicUrl { get; set; }
        public string RelativePath { get; set; }
    }

    public class ThreadList
    {
        [Key]
        public int? ThreadId { get; set; }
        public string? CommentText { get; set; }
        public string? HtmlDesc { get; set; }
        public Guid Issue_Id { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? CreatedAt { get; set; }
        public Guid? UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? From_Time { get; set; }
        public DateTime? To_Time { get; set; }
        public string? Hours { get; set; }

    }

    public class IssueRepositoryInfo
    {
        [Key]
        public Guid? RepoId { get; set; }
        public string? RepoKey { get; set; }
        public string? IssueTitle { get; set; }

    }

    public class ProjectKeysDto
    {
        public string RepoKey { get; set; }
        public string ProjectKey { get; set; }
    }
}
