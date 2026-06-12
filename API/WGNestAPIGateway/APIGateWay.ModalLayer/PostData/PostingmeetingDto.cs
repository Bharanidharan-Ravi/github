using APIGateWay.ModalLayer.Helper;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static APIGateWay.ModalLayer.Helper.PostHelper;

namespace APIGateWay.ModalLayer.PostData
{
    public class PostingmeetingDto
    {
        public string? host_type { get; set; }
        public Guid? host_id { get; set; }
        public string? title { get; set; }
        public string? meet_method { get; set; }
        public string ?meet_password { get; set; }
        public string? meet_link { get; set; }
        public Guid ?ticket_id { get; set; }
        public Guid? project_id { get; set; }
        public DateOnly? valid_from_date { get; set; }
        public DateOnly? meeting_Date { get; set; }
        public DateOnly? valid_to_date { get; set; }
        public string ?start_time { get; set; }
        public string? end_time { get; set; }
        public string? time_zone_id { get; set; }
        public string? days_of_week { get; set; }
        public string? recurrence_type { get; set; }
        public string? slot_duration { get; set; }
        public string? booking_type { get; set; }
        public string? meeting_summary { get; set; }
        public List<InternalParticipants>? internalParticipants { get; set; }
        public List<ClientParticipants>? clientParticipants { get; set; }

    }
    public class InternalParticipants
    {
        public Guid? Id { get; set; }
    }  
    public class ClientParticipants
    {
        public Guid? Id { get; set; }
    }
    public class MeetingAttendance 
    {
        [Key]
        public int? attendance_id { get; set; }
        public Guid hoster_id { get; set; }
       
        public Guid meeting_id { get; set; }

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
}
