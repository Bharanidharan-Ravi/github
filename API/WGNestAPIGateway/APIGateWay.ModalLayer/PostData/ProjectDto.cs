using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static APIGateWay.ModalLayer.Helper.PostHelper;

namespace APIGateWay.ModalLayer.PostData
{
    public class ProjectDto
    {
        public string Title { get; set; }
        public string Description { get; set; }
        //public string RepoKey { get; set; }
        public Guid? Responsible { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? DueDate { get; set; }
        public Guid? Repo_Id { get; set; }
        public string? HtmlDesc { get; set; }
        public TempReturn? temp { get; set; }
    }

    // ── PUT /api/project/{id} ─────────────────────────────────────────────────
    // Full project update. Include Repo_Id so RepoScopeHandler can validate
    // scope directly from body — saves one extra DB lookup.
    // If you omit Repo_Id, handler automatically falls back to entity lookup.
    public class UpdateProjectDto
    {
        public Guid? Repo_Id { get; set; }   // for scope validation
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }   // rich HTML from editor
        public int? Status { get; set; }   // optional — change in same call
        public DateTime? DueDate { get; set; }
        public TempReturn? temp { get; set; }   // new file uploads if any
    }

    // ── PATCH /api/project/{id}/status ────────────────────────────────────────
    // ── PATCH /api/ticket/{id}/status  ────────────────────────────────────────
    // ── PATCH /api/thread/{id}/status  ────────────────────────────────────────
    // Body is always: { "Status": 2 }
    // No Repo_Id in body — RepoScopeHandler looks up entity's Repo_Id from DB
    // using the {id} route param and validates it. All handled before controller runs.
    public class UpdateStatusDto
    {
        public int Status { get; set; }
    }
}
