using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.GETData
{
    public class GetMeeting
    {
        [Key]
        public Guid meeting_id { get; set; }

        public string? host_type { get; set; }
        public string?HostName { get; set; }
        public Guid host_id { get; set; }
        public string? title { get; set; }
        public string? meet_method { get; set; }
        public string? meet_password { get; set; }
        public string? meet_link { get; set; }
        public Guid ?ticket_id { get; set; }
        public string ? Ticket_Title { get; set; }
        public Guid? project_id { get; set; }
        public DateOnly ?valid_from_date { get; set; }
        public DateOnly? valid_to_date { get; set; }
        public string? start_time { get; set; }
        public string? end_time { get; set; }
        public string ?time_zone_id { get; set; }
        public string? days_of_week { get; set; }
        public string? recurrence_type { get; set; }
        public string? slot_duration { get; set; }
        public string? booking_type { get; set; }
        public string? status { get; set; }
        public string? InternalParticipants { get; set; }
        public string? ClientParticipants { get; set; }
        public string? meeting_summary { get; set; }
        public Guid created_by { get; set; }
        public Guid updated_by { get; set; }
        public DateTime created_at { get; set; }
        public DateTime updated_at { get; set; }
    }


    public class GetUpcomingMeeting
    {
        [Key]
        public Guid meeting_id { get; set; }
        public string? title { get; set; }
        public string? MeetingType { get; set; }
        public string? Date { get; set; }
        public string? Time { get; set; }
        public decimal? DurationHours { get; set; }
        public string? DaysOfWeek { get; set; }
        public string? Organizer { get; set; }
        public string? status { get; set; }
    } 


}
