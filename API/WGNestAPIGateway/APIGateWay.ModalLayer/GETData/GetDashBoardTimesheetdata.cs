
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.GETData
{
    public class DashBoardTimeSheetData
    {
        [Key]
        public long? RowNum { get; set; }
        public long? ThreadId { get; set; }
        public Guid? Issue_Id { get; set; }
        public Guid? EmployeeId { get; set; }

        public string? EmployeeName { get; set; }

        public string? Repository_Name { get; set; }

        public string? Project_Name { get; set; }
        public Guid? Project_Id { get; set; }
        public Guid? UpdatedBy { get; set; }

        public string? TicketNo { get; set; }

        public string? TicketName { get; set; }
        public int? Status { get; set; }

        public string? EstimatedHours { get; set; }
        public string? EntireConsumeTime { get; set; }

        public string? ConsumeTime { get; set; }
        public decimal? CompletionPct { get; set; }
        public DateTime? Due_Date { get; set; }
        public Guid? RepoId { get; set; }
        public string? RepoKey { get; set; }
        public string? Comment { get; set; }
        public string? Labels_JSON { get; set; }
        public DateTime? StartTime { get; set; }

        public DateTime? EndTime { get; set; }


        public string? total { get; set; }
        public string? ThreadStatusName { get; set; }
        public int? ThreadStatusId { get; set; }
        public decimal? OverallPercentage { get; set; }
        public bool? IsPrivate { get; set; }
        public string? CurrentStatusSummary { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? CreatedAt { get; set; }

        public string? CreatedByName { get; set; }
    }

    // Row of GetTimesheet_V2 (config key "TimesheetV2"): timesheet entries only,
    // the UI loads the tickets from "TicketListV2" by Issue_Id.
    //   EntryType: WORK | MEETING | STATUS (closed / reopened without a thread)
    //   Action   : "C" closed / "R" reopened, NULL = plain entry
    public class GetTimesheetRow
    {
        public string Id { get; set; } = string.Empty;
        public string EntryType { get; set; } = string.Empty;
        public string? Action { get; set; }
        public Guid Issue_Id { get; set; }
        public long? ThreadId { get; set; }
        public Guid? MeetingId { get; set; }
        public Guid? EmployeeId { get; set; }
        public bool IsCoContributor { get; set; }
        public DateTime WorkDate { get; set; }
        public DateTime EventAt { get; set; }
        public DateTime? From_Time { get; set; }
        public DateTime? To_Time { get; set; }
        public int? Minutes { get; set; }
        public int? NewStatusId { get; set; }
        public string? Comment { get; set; }
        public string? CurrentStatusSummary { get; set; }
    }
}
