using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.MasterData
{
    public class MeetingMaster
    {
        [Key]
        public Guid meeting_id { get; set; }
        public string host_type { get; set; }
        public Guid host_id { get; set; }
        public string title { get; set; }
        public string meet_method { get; set; }
        public string meet_password { get; set; }
        public string meet_link { get; set; }
        public Guid ticket_id { get; set; }
        public Guid project_id { get; set; }
        public DateOnly valid_from_date { get; set; }
        public DateOnly valid_to_date { get; set; }
        public string start_time { get; set; }
        public string end_time { get; set; }
        public string time_zone_id { get; set; }
        public string days_of_week { get; set; }
        public string recurrence_type { get; set; }
        public string slot_duration { get; set; }
        public string booking_type { get; set; }
        public string status { get; set; }
        public string meeting_summary { get; set; }
        public Guid created_by { get; set; }
        public Guid updated_by { get; set; }
        public DateTime created_at { get; set; }
        public DateOnly? meeting_date { get; set; }
        public DateTime updated_at { get; set; }
    }
}
