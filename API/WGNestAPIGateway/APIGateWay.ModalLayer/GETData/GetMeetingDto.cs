using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.GETData
{
    public class GetMeetingDto
    {
        public Guid Meeting_Id { get; set; }
        public string Title { get; set; }
        public string Host_Type { get; set; }
        public string? Host_Name { get; set; }
        public string? Ticket_Title { get; set; }
        public Guid? Ticket_Id { get; set; }
        public long? ThreadId { get; set; }
        public Guid Host_Id { get; set; }
        public string? Booking_Type { get; set; }
        public Guid? Project_Id { get; set; }
        public string? Project_Name { get; set; }
        public string? Meeting_Summary { get; set; }
        public string? Slot_Duration { get; set; }
        public string Recurrence_Type { get; set; }

        public DateTime? Meeting_Date { get; set; }
        public DateTime? Valid_From_Date { get; set; }
        public DateTime? Valid_To_Date { get; set; }
        public string? Start_Time { get; set; }
        public string? End_Time { get; set; }

        public string? Days_Of_Week { get; set; } // Returns as "1,3,5"
        public string Status { get; set; } // e.g., "Scheduled", "In-Progress"
        public DateTime Created_At { get; set; }
        public DateTime? Updated_At { get; set; }
        public Guid? Updated_By { get; set; }

        // Nested lists for the attendees so the UI can render the tags/status
        //public List<GetAttendeeDto> InternalParticipants { get; set; } = new List<GetAttendeeDto>();
        //public List<GetAttendeeDto> ClientParticipants { get; set; } = new List<GetAttendeeDto>();
        public string? InternalParticipants { get; set; }
        public string? ClientParticipants { get; set; }
    }

    public class GetAttendeeDto
    {
        [Key]
        public Guid Participant_Id { get; set; }
        public string? Participant_Name { get; set; }
        public string? Participant_Role { get; set; } // "Host" or "Participant"
        public string? Invite_Status { get; set; } // "Pending", "Accepted", "Declined"
        public string? Attendance_Status { get; set; }
    }

}
