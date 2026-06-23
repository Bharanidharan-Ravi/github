using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.GETData
{
    public class GetStaleTicketsForAssignee
    {
       public int? SiNo { get; set; }
       public string? Issue_Code { get; set; }
       public string? Title { get; set; }
       public string? Description { get; set; }
       public string? Hours { get; set; }
       public DateTime? Due_Date { get; set; }
       public string? RepoKey { get; set; }
       public string? ProjKey { get; set; }
        [Key]
       public Guid? Assignee_Id { get; set; }
       public int? Status { get; set; }
       public Guid? Issue_Id { get; set; }
       public DateTime? IssueMaster_UpdatedAt { get; set; }
       public Guid? UpdatedBy { get; set; }
       public Guid? Project_Id { get; set; }
       public string? Priority { get; set; }
       public string? Repo_Name { get; set; }
       public string? Proj_Name { get; set; }
       public string? StatusName { get; set; }
       public decimal? CompletionPct { get; set; }
       public DateTime? Thread_LastUpdated { get; set; }
       public DateTime? ProgressLog_CreatedDate { get; set; }
       public DateTime? LatestUpdateAcrossTables { get; set; }
       public int? DaysSinceLastUpdate { get; set; }

    }
}
