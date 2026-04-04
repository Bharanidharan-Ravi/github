using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.GETData
{
    public class GetDailyPlan
    {
        public int Id { get; set; }
        public Guid TicketId { get; set; }
        public string? ProjKey { get; set; }
        public string? RepoKey { get; set; }
        public DateTime PlannedDate { get; set; }
        public int Status { get; set; }
        //public string StatusLabel { get; set; } = string.Empty;

        //// true when Status=2 — UI disables the checkbox
        //public bool IsLocked { get; set; }

        public string? UncheckComment { get; set; }

        // Joined from TicketMaster for display
        public string? Title { get; set; }
        public string? Issue_Code { get; set; }
        //public string? ProjectName { get; set; }

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public Guid UserId { get; set; }
        public string? Priority { get; set; }
        public string? Labels_JSON { get; set; }
        public string project { get; set; }
        public Guid Project_ID { get; set; }
        public string Repo_Name { get; set; }
        public Guid Repo_Id { get; set; }

        public string? All_Assignees { get; set; }
        public DateTime? Due_Date { get; set; }
        public decimal? CompletionPct { get; set; }
    }
}
