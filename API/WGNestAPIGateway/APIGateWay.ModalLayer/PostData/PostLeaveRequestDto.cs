using System;

namespace APIGateWay.ModalLayer.PostData
{
    public class PostLeaveRequestDto
    {
        public DateTime LeaveFrom { get; set; }
        public DateTime LeaveTo { get; set; }
        public string LeaveTypeId { get; set; }
        public string? Comments { get; set; }
    }
}
