// NEW FILE  (same folder as RegisterRequestDto)
using System;

namespace APIGateWay.ModalLayer.DTOs
{
    // sent back after username + password are correct
    public class LoginOtpResponse
    {
        public bool OtpRequired { get; set; } = true;
        public Guid OtpId { get; set; }
        public string MaskedEmail { get; set; } = "";     // e.g. a*****@gmail.com
        public int ExpiresInSeconds { get; set; }
    }

    // sent by the React page to verify the code
    public class OtpVerifyRequest
    {
        public Guid OtpId { get; set; }
        public string Otp { get; set; } = "";
        public string? DeviceInfo { get; set; }
    }

    // sent by the React page to ask for a new code
    public class OtpResendRequest
    {
        public Guid OtpId { get; set; }
    }
}
