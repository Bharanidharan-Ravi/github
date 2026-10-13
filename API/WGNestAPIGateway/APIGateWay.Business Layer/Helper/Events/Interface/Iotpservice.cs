// NEW FILE  (same folder as IClientMailNotifier.cs)
using System;
using System.Threading.Tasks;
using APIGateWay.ModalLayer.DTOs;

namespace APIGateWay.BusinessLayer.Helper.Events.Interface
{
    public interface IOtpService
    {
        /// Create an OTP for this user and email it.
        Task<LoginOtpResponse> SendLoginOtpAsync(Guid userId, string? deviceInfo, string? ip);

        /// Send a fresh code for an existing otpId.
        Task<LoginOtpResponse> ResendAsync(Guid otpId);

        /// Check the code. Returns the UserId when correct, throws when wrong or expired.
        Task<Guid> VerifyAsync(Guid otpId, string otp);
    }
}
