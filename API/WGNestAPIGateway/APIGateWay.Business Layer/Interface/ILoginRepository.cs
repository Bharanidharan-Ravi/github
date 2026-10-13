using APIGateWay.ModalLayer.DTOs;
using APIGateWay.ModalLayer.GETData;
using System;

namespace APIGateWay.BusinessLayer.Interface
{
    public interface ILoginRepository
    {
        Task<GetUserList> RegisterUserAsync(RegisterRequestDto request);
        //Task<string> GetUserinfo(string username, string password, string deviceInfo);
        Task<LoginOtpResponse> GetUserinfo(string username, string password, string deviceInfo);  // CHANGED (was Task<string>)
        Task<string> VerifyOtpAndLogin(OtpVerifyRequest request);                                 // NEW, returns the JWT
        Task<LoginOtpResponse> ResendOtp(Guid otpId);                                             // NEW

        Task LogoutSession(Guid sessionId);
        Task UpdateHeartbeat(Guid sessionId);
    }
}
