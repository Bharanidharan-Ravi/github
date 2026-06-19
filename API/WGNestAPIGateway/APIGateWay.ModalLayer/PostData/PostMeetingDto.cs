using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.PostData
{
    public class PostMeetingDto
    {
        [Required]
        [MaxLength(255)]
        public string Title { get; set; }

        [Required]
        public string Host_Type { get; set; } // "Employee" or "Client"

        [Required]
        public Guid Host_Id { get; set; }
        public Guid? Ticket_id { get; set; }

        [Required]
        public string Booking_Type { get; set; } // "Meeting", "Interview", etc.

        public Guid? Project_Id { get; set; }

        public string? Meeting_Summary { get; set; }

        [Required]
        public string Slot_Duration { get; set; } // Stored in minutes (e.g., 30, 60)

        [Required]
        public string Recurrence_Type { get; set; } // "ONETIME", "DAILY", "WEEKLY"

        // Nullable dates because they depend on the Recurrence_Type
        public DateTime? Meeting_Date { get; set; }
        public DateTime? Valid_From_Date { get; set; }
        public DateTime? Valid_To_Date { get; set; }

        [Required]
        public string Start_Time { get; set; }

        [Required]
        public string End_Time { get; set; }
        public string? Days_Of_Week { get; set; }
        public List<SelectionItem> InternalParticipants { get; set; } = new List<SelectionItem>();
        public List<SelectionItem> ClientParticipants { get; set; } = new List<SelectionItem>();
    }

    // Helper class to catch the { id, name } object structure from your React dropdowns
    public class SelectionItem
    {
        public Guid Id { get; set; }

    }

    public class MeetingAttendance
    {
        [Key]
        public int attendance_id { get; set; }
        public Guid? meeting_id { get; set; }
        public string? participant_type { get; set; }
        public Guid participant_id { get; set; }
        public string? participant_role { get; set; }
        public string? invite_status { get; set; }
        public string? attendance_status { get; set; }
        public DateTime? response_date { get; set; }
        public string? remark { get; set; }
        public Guid? created_by { get; set; }
        public DateTime? created_at { get; set; }
    }

    public class PutMeetingDto
    {
        [Required]
        public Guid Meeting_Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string Title { get; set; }

        [Required]
        public string Host_Type { get; set; }

        [Required]
        public Guid Host_Id { get; set; }

        [Required]
        public string Booking_Type { get; set; }

        public Guid? Project_Id { get; set; }
        public string Meeting_Summary { get; set; }
        public string Slot_Duration { get; set; }
        public string Recurrence_Type { get; set; }
        public DateTime? Meeting_Date { get; set; }
        public DateTime? Valid_From_Date { get; set; }
        public DateTime? Valid_To_Date { get; set; }
        public string Start_Time { get; set; }
        public string End_Time { get; set; }
        public string Status { get; set; } // Allow editing status (e.g., "Cancelled", "Completed")

        public string? Days_Of_Week { get; set; }
        public List<SelectionItem> InternalParticipants { get; set; } = new List<SelectionItem>();
        public List<SelectionItem> ClientParticipants { get; set; } = new List<SelectionItem>();
    }
}
