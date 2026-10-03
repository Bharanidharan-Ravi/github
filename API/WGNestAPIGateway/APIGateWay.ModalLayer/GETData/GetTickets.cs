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
        public string? Issuer_Name { get; set; }
        public DateTime? CreatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public Guid? ReopenedBy { get; set; }
        public Guid Project_Id { get; set; }
        public Guid? RepoId { get; set; }
        public string? RepoKey { get; set; }
        public string? TicketCreater { get; set; }
        public string? commenttext { get; set; }
        public decimal? CompletionPct { get; set; }
        public Guid? Assignee_Id { get; set; }
        public string? Assignee_Name { get; set; }
        public string? All_Assignees { get; set; }
        //public string? HandOffs { get; set; }
        public string? Priority { get; set; }
        public DateTime? Due_Date { get; set; }
        public int? StatusId { get; set; }
        public string? Issue_Code { get; set; }
        public string? Status { get; set; }
        public string? Hours { get; set; }
        public string? TotalConsumeTime { get; set; }
        public string? Labels_JSON { get; set; }
        public string? Move_toJson { get; set; }
        public string? Attachment_JSON { get; set; }
        public decimal? OverallPercentage { get; set; }
        public string? CurrentStatusSummary { get; set; }
        public bool IsCloseRequested { get; set; }
        public string? Web { get; set; }
        public bool PriorityRequest { get; set; }
        public bool FuncResponse { get; set; }
        public bool WebResponse { get; set; }
        public bool TechnicalResponse { get; set; }
        public bool AdminResponse { get; set; }
        public string? Technical { get; set; }
        public string? Functional { get; set; }
        public string? Client { get; set; }
        public bool? RaiseToClient { get; set; }
        public int? ThreadCount { get; set; }
        public bool? IsPrivate { get; set; }
        //public List<GetLabelForIssues> Labels_JSON { get; set; }
        //public List<GetAttachForIssues> Attachment_JSON { get; set; }
    }


    // Row of GetTicketList_V2 (config key "TicketListV2"): ids only, the UI maps
    // names from its masters. *_Ids are comma-separated lower-case ids.
    public class GetTicketListRow
    {
        [Key]
        public Guid Issue_Id { get; set; }
        public string? Issue_Code { get; set; }
        public string? Title { get; set; }
        public int? StatusId { get; set; }
        public string? Priority { get; set; }
        public bool? IsPrivate { get; set; }
        public bool RaiseToClient { get; set; }
        public string? ProjKey { get; set; }
        public string? RepoKey { get; set; }
        public Guid? Project_Id { get; set; }
        public Guid? RepoId { get; set; }
        public Guid? Assignee_Id { get; set; }
        public Guid CreatedBy { get; set; }
        public DateTime? CreatedAt { get; set; }
        public Guid? UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? Due_Date { get; set; }
        public string? Hours { get; set; }
        public decimal? CompletionPct { get; set; }
        public decimal? OverallPercentage { get; set; }
        public bool IsCloseRequested { get; set; }
        public bool PriorityRequest { get; set; }
        public bool FuncResponse { get; set; }
        public bool WebResponse { get; set; }
        public bool TechnicalResponse { get; set; }
        public bool AdminResponse { get; set; }
        public Guid? ReopenedBy { get; set; }
        public int ThreadCount { get; set; }
        public int? TotalConsumeMinutes { get; set; }
        public int? TeamConsumeMinutes { get; set; }
        public string? commenttext { get; set; }
        public string? Label_Ids { get; set; }
        public string? Assignee_Ids { get; set; }
        public string? Handler_Ids { get; set; }
    }

    // Row of GetTicketListCounts_V2 (config key "TicketListCountsV2").
    // Facet "total" has Value null; other facets use the filter value format.
    public class GetTicketListCount
    {
        [Key]
        public string Id { get; set; } = "";
        public string Facet { get; set; } = "";
        public string? Value { get; set; }
        public int Cnt { get; set; }
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
    public class GetIssueLogList
    {
        // Keyed on IssueLogId: several issues share one ThreadId when they are
        // submitted together.
        public Guid Issue_Id { get; set; }
        [Key]
        public int IssuelogId { get; set; }
        public string? Description { get; set; }

        public string? Status { get; set; }

        // Latest ISSUEACTIVITY row for the issue.
        public string? Activities { get; set; }



    }
    public class ThreadList
    {
        [Key]
        public long? ThreadId { get; set; }
        public string? CommentText { get; set; }
        public string? HtmlDesc { get; set; }
        public Guid Issue_Id { get; set; }
        public int? HandsOffId { get; set; }
        public Guid CreatedId { get; set; }
        public Guid? WorkStreamId { get; set; }
        public string? CreatedBy { get; set; }
        public decimal? CompletionPct { get; set; }
        public string? CoContributors_JSON { get; set; }
        public string? Reactions_JSON { get; set; }
        public string? MeetingDetails_JSON { get; set; }
        public DateTime? CreatedAt { get; set; }
        public Guid? UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? From_Time { get; set; }
        public DateTime? To_Time { get; set; }
        public string? Hours { get; set; }
        public string? Ref_Id { get; set; }
        public string? ThreadType { get; set; }
        public Guid? MeetingId { get; set; }
        public int? team { get; set; }
        public bool? toClient { get; set; }
        public string? AdminFeedback { get; set; }
        public int? AdminRating { get; set; }
        public string? ThreadFor { get; set; }
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
