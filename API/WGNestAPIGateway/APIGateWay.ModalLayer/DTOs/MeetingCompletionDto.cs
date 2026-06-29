using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.DTOs
{
    public class MeetingCompletionDto
    {
        public Guid MeetingId { get; set; }

        public DateTime ActualStartTime { get; set; }

        public DateTime ActualEndTime { get; set; }

        public string MeetingSummary { get; set; }
        public List<MeetingAttendanceUpdateDto> Attendance { get; set; } = new();
    }
    public class MeetingAttendanceUpdateDto
    {
        public Guid ParticipantId { get; set; }

        public string AttendanceStatus { get; set; } = string.Empty;

        public string? InviteStatus { get; set; }

        public string? Remark { get; set; }
    }
}
