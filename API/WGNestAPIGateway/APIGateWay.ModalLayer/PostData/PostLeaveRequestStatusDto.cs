using System;
using System.Collections.Generic;

namespace APIGateWay.ModalLayer.PostData
{
    public class PostLeaveRequestStatusDto
    {
        // "APPROVED" or "REJECTED"
        public string Status { get; set; }
        public string? RejectReason { get; set; }

        // Optional admin re-adjustment of the leave dates. When both are sent the
        // request's day rows are rebuilt from them (dates not listed in Days are FULL).
        public DateTime? LeaveFrom { get; set; }
        public DateTime? LeaveTo { get; set; }
        public List<PostLeaveDayDto>? Days { get; set; }
    }
}
