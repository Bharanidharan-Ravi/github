using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.MasterData
{
    [Table("MeetingCompletion")]
    public class MeetingCompletion
    {
        [Key]
        public Guid completion_id { get; set; }

        public Guid meeting_id { get; set; }

        public DateTime? actual_start_time { get; set; }

        public DateTime? actual_end_time { get; set; }

        public int? duration_minutes { get; set; }

        public string? meeting_summary { get; set; }

        public Guid? completedby { get; set; }

        public DateTime completedat { get; set; }

        public DateTime createdAt { get; set; }

        public DateTime updatedAt { get; set; }

        // The day the meeting ran on
        [Column(TypeName = "date")]
        public DateTime occurrence_date { get; set; }
    }
}
