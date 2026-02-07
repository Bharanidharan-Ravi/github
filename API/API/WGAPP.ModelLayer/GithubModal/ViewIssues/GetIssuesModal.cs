using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WGAPP.ModelLayer.GithubModal.TicketingModal;

namespace WGAPP.ModelLayer.GithubModal.ViewIssues
{
    public class IssuesThread
    {
        [Key]
        public int? ThreadId { get; set; }
        public Guid Issue_Id { get; set; }
        public string? IssueTitle { get; set; }
        public string? CommentText { get; set; }
        public Guid CommentedBy { get; set; }
        public DateTime? CommentedAt { get; set; }
        public Guid? UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? Ref_Id { get; set; }
    }

    public class IssueThreadDTO
    {
        public IssuesThread thread { get; set; }
        public TempReturn TempReturns { get; set; }
    }
    public class GetThreadModal
    {
        public int? ThreadId { get; set; }
        public int? Issue_Id { get; set; }
        public string? CommentText { get; set; }
        public int CommentedBy { get; set; }
        public DateTime CommentedAt { get; set; }
        public int UpdatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? Repo_Id { get; set; }
        public string? threadCode { get; set; }
        public string? attachment_id { get; set; }
        public string? attachment_title { get; set; }
        public Byte[]? attachment_file { get; set; }
        public string? Ref_Id { get; set; }
    }

    public class ThreadbyTicketId
    {
        public GetAllIssueData issuesData { get; set; }
        public List<ThreadCommentDto> threadData { get; set; }
    }

    public class ThreadCommentDto
    {
        public int ThreadId { get; set; }
        public string CommentText { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CommentedAt { get; set; }
        public Guid? UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // JSON stored as string
        public List<GETATTACHFORISSUES> Attachment_JSON { get; set; }
    }
}
