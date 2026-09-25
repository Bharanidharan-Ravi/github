using System;

namespace APIGateWay.ModalLayer.PostData
{
    public class PostPermissionRequestDto
    {
        public DateTime PermissionDate { get; set; }
        public int DurationMinutes { get; set; }
        public string? Remarks { get; set; }
    }
}
