using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.PostData
{
    // Input for POST /api/workstream (individual stream post)
    public class PostWorkStreamDto
    {
        public Guid IssueId { get; set; }
        public Guid? ResourceId { get; set; }  // null = current user

        // StreamName from UI dropdown (user picks their stage explicitly)
        //public string StreamName { get; set; } = string.Empty;

        // StatusId from Status_Master — sent from UI
        // e.g. user picks "In Development" → UI sends 5
        public int? StreamStatus { get; set; }

        // Toggle button:
        //   true  = link last thread of this user for this ticket
        //   false = create new ThreadMaster row from Comment
        public bool UseLastThread { get; set; } = false;
        public string? Comment { get; set; }  // required when UseLastThread=false

        public decimal? CompletionPct { get; set; }
        public DateTime? TargetDate { get; set; }
        public long? ParentThreadId { get; set; }
    }
}
