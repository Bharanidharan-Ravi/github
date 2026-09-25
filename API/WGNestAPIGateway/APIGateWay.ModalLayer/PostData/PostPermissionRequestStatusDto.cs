namespace APIGateWay.ModalLayer.PostData
{
    public class PostPermissionRequestStatusDto
    {
        // "APPROVED" or "REJECTED"
        public string Status { get; set; }
        public string? RejectReason { get; set; }
    }
}
