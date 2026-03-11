using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.PostData
{
    public class PostTicketDto
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Hours { get; set; }
        //public string RepoKey { get; set; }
        public Guid? Project_Id { get; set; }

        public DateTime? Due_Date { get; set; }
        public Guid? Assignee_Id { get; set; }
        public Guid? RepoId { get; set; }
        public string? HtmlDesc { get; set; }
        public TempReturn? temp { get; set; }
        public List<LabelData>? labelId { get; set; }
        public List<Assignees>? assignees { get; set; }
    }

    public class Assignees
    {
        public Guid? Id { get; set; }
    }
    public class LabelData
    {
        public int? Id { get; set; }
    }

    public class UpdateTicketDto
    {
        public Guid? RepoId { get; set; }   // matches PostTicketDto.RepoId
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }   // HTML from rich editor
        public int? Status { get; set; }   // optional — change in same call
        public int? Priority { get; set; }
        public Guid? AssigneeId { get; set; }
        public DateTime? DueDate { get; set; }
        public TempReturn? temp { get; set; }   // new file uploads if any

        // Same shape as PostTicketDto.labelId — full replacement list on update
        // null   → labels not touched
        // empty  → all labels removed
        // filled → delete old labels, insert these
        public List<LabelData>? labelId { get; set; }
    }

    // ── PATCH /api/ticket/{id}/status ─────────────────────────────────────────
    // Body: { "Status": 2 }   No RepoId needed.
    // RepoScopeHandler looks up ticket's Repo_Id from DB by {id} route param.
    public class UpdateTicketStatusDto
    {
        public int Status { get; set; }
    }
}
