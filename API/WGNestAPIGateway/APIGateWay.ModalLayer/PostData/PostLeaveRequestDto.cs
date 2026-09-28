using System;
using System.Collections.Generic;

namespace APIGateWay.ModalLayer.PostData
{
    public class PostLeaveRequestDto
    {
        public DateTime LeaveFrom { get; set; }
        public DateTime LeaveTo { get; set; }
        public string LeaveTypeId { get; set; }
        public string? Comments { get; set; }

        // Day-wise sessions. Dates in the range that aren't listed are taken as FULL.
        public List<PostLeaveDayDto>? Days { get; set; }
    }

    public class PostLeaveDayDto
    {
        public DateTime Date { get; set; }

        // FULL | FIRST_HALF | SECOND_HALF
        public string Session { get; set; }
    }
}
