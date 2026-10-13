// NEW FILE  (same folder as LOGIN_MASTER.cs)
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APIGateWay.ModalLayer.MasterData
{
    [Table("WGUserOtp")]
    public class UserOtp
    {
        [Key]
        public Guid OtpId { get; set; }
        public Guid UserId { get; set; }
        public string OtpHash { get; set; } = "";
        public string Salt { get; set; } = "";
        public string? DeviceInfo { get; set; }
        public string? IpAddress { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public int Attempts { get; set; }
        public int ResendCount { get; set; }
        public bool IsUsed { get; set; }
    }
}
