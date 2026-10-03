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

        // DAILY / WEEKLY only: the day being written up ("yyyy-MM-dd"). The
        // comment is posted as a new thread on the ticket (never replacing an
        // earlier one for that day) and the meeting stays Scheduled, unless
        // EndSeries is also set.
        public DateTime? OccurrenceDate { get; set; }

        // With OccurrenceDate: also complete the whole series.
        public bool EndSeries { get; set; }
    }
    public class MeetingAttendanceUpdateDto
    {
        public Guid ParticipantId { get; set; }

        public string AttendanceStatus { get; set; } = string.Empty;

        public string? InviteStatus { get; set; }

        public string? Remark { get; set; }
    }
}
