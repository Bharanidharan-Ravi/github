// NEW FILE  (same folder as ClientMailNotifier.cs)
using System;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using APIGateWay.BusinessLayer.Helper.Events.Interface;
using APIGateWay.DomainLayer.DBContext;
using APIGateWay.ModalLayer.DTOs;
using APIGateWay.ModalLayer.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace APIGateWay.BusinessLayer.Helper.Events.Services
{
    public class OtpService : IOtpService
    {
        // ---- rules (change here if you want different limits) ----
        private const int ExpiryMinutes = 5;
        private const int MaxAttempts = 5;
        private const int MaxResends = 3;
        private const int ResendCooldownSec = 30;

        private readonly APIGatewayDBContext _db;
        private readonly IConfiguration _cfg;

        public OtpService(APIGatewayDBContext db, IConfiguration cfg)
        {
            _db = db;
            _cfg = cfg;
        }

        // ------------------------------------------------------------------
        // 1) create + send
        // ------------------------------------------------------------------
        public async Task<LoginOtpResponse> SendLoginOtpAsync(Guid userId, string? deviceInfo, string? ip)
        {
            var email = await ResolveEmailAsync(userId);
            if (string.IsNullOrWhiteSpace(email))
                throw new Exception("No valid email is registered for this user. Please contact Admin.");

            // only one open OTP per user: close the old ones
            var open = await _db.Set<UserOtp>().Where(o => o.UserId == userId && !o.IsUsed).ToListAsync();
            foreach (var o in open) o.IsUsed = true;

            var row = new UserOtp { OtpId = Guid.NewGuid(), UserId = userId, DeviceInfo = deviceInfo, IpAddress = ip };
            var otp = Issue(row);
            _db.Set<UserOtp>().Add(row);
            await _db.SaveChangesAsync();

            await SendMailAsync(email, otp);
            return Build(row, email);
        }

        // ------------------------------------------------------------------
        // 2) resend
        // ------------------------------------------------------------------
        public async Task<LoginOtpResponse> ResendAsync(Guid otpId)
        {
            var row = await _db.Set<UserOtp>().FirstOrDefaultAsync(o => o.OtpId == otpId);
            if (row == null || row.IsUsed)
                throw new Exception("Login session expired. Please login again.");
            if (row.ResendCount >= MaxResends)
                throw new Exception("Resend limit reached. Please login again.");
            if ((DateTime.UtcNow - row.CreatedAt).TotalSeconds < ResendCooldownSec)
                throw new Exception("Please wait a few seconds before requesting another OTP.");

            var email = await ResolveEmailAsync(row.UserId);
            if (string.IsNullOrWhiteSpace(email))
                throw new Exception("No valid email is registered for this user. Please contact Admin.");

            var otp = Issue(row);          // new code, new expiry, attempts reset
            row.ResendCount++;
            await _db.SaveChangesAsync();

            await SendMailAsync(email, otp);
            return Build(row, email);
        }

        // ------------------------------------------------------------------
        // 3) verify
        // ------------------------------------------------------------------
        public async Task<Guid> VerifyAsync(Guid otpId, string otp)
        {
            var row = await _db.Set<UserOtp>().FirstOrDefaultAsync(o => o.OtpId == otpId);
            if (row == null || row.IsUsed)
                throw new Exception("Invalid or expired OTP. Please login again.");

            if (DateTime.UtcNow > row.ExpiresAt)
            {
                row.IsUsed = true;
                await _db.SaveChangesAsync();
                throw new Exception("OTP expired. Please login again.");
            }

            if (row.Attempts >= MaxAttempts)
            {
                row.IsUsed = true;
                await _db.SaveChangesAsync();
                throw new Exception("Too many wrong attempts. Please login again.");
            }

            row.Attempts++;
            var expected = Encoding.UTF8.GetBytes(row.OtpHash);
            var actual = Encoding.UTF8.GetBytes(Hash((otp ?? "").Trim(), row.Salt));
            if (!CryptographicOperations.FixedTimeEquals(expected, actual))
            {
                await _db.SaveChangesAsync();
                throw new Exception("Invalid OTP.");
            }

            row.IsUsed = true;             // single use
            await _db.SaveChangesAsync();
            return row.UserId;
        }

        // ==================================================================
        // helpers
        // ==================================================================

        // make a new random code and store only its hash on the row
        private static string Issue(UserOtp row)
        {
            var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();   // 6 digits
            var salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
            row.Salt = salt;
            row.OtpHash = Hash(otp, salt);
            row.CreatedAt = DateTime.UtcNow;
            row.ExpiresAt = row.CreatedAt.AddMinutes(ExpiryMinutes);
            row.Attempts = 0;
            return otp;
        }

        private static string Hash(string otp, string salt)
        {
            using var h = new HMACSHA256(Convert.FromBase64String(salt));
            return Convert.ToBase64String(h.ComputeHash(Encoding.UTF8.GetBytes(otp)));
        }

        private static LoginOtpResponse Build(UserOtp row, string email) => new()
        {
            OtpId = row.OtpId,
            MaskedEmail = Mask(email),
            ExpiresInSeconds = (int)(row.ExpiresAt - DateTime.UtcNow).TotalSeconds
        };

        private static string Mask(string email)
        {
            var at = email.IndexOf('@');
            if (at <= 1) return email;
            return email[0] + new string('*', Math.Min(at - 1, 6)) + email[at..];
        }

        // Who logs in -> where the OTP goes:
        //   Employee user -> EMPLOYEEMASTER.Email  (EmployeeID = WGUserDetails.UserID)
        //   Repo user     -> RepoUsers.MailId      (RepoUsers.UserId = WGUserDetails.UserID, Status = 'Active')
        private async Task<string?> ResolveEmailAsync(Guid userId)
        {
            var email = await _db.Set<EMPLOYEEMASTER>().AsNoTracking()
                .Where(e => e.EmployeeID == userId)
                .Select(e => e.Email)
                .FirstOrDefaultAsync();

            if (!IsValidEmail(email))
            {
                email = await _db.RepoUsers.AsNoTracking()
                    .Where(u => u.UserId == userId && u.Status == "Active")
                    .Select(u => u.MailId)
                    .FirstOrDefaultAsync();
            }

            return IsValidEmail(email) ? email!.Trim() : null;
        }

        // your tables contain junk like "string", "aaaa", "1222": never try to send to those
        private static bool IsValidEmail(string? e)
        {
            if (string.IsNullOrWhiteSpace(e)) return false;
            try { return new MailAddress(e.Trim()).Address == e.Trim(); }
            catch { return false; }
        }

        // Unlike ClientMailNotifier this must NOT swallow errors: no mail = no login.
        private async Task SendMailAsync(string toEmail, string otp)
        {
            try
            {
                var s = _cfg.GetSection("Smtp");
                using var msg = new MailMessage
                {
                    From = new MailAddress(s["From"]!),
                    IsBodyHtml = true,
                    Subject = "Your login OTP",
                    Body = $@"<p>Your one-time password is:</p>
                              <h2 style='letter-spacing:4px'>{otp}</h2>
                              <p>It is valid for {ExpiryMinutes} minutes. Do not share it with anyone.</p>"
                };
                msg.To.Add(toEmail);

                using var smtp = new SmtpClient(s["Host"], int.Parse(s["Port"]!))
                {
                    EnableSsl = true,
                    Credentials = new NetworkCredential(s["User"], s["Password"])
                };
                await smtp.SendMailAsync(msg);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OtpMail] failed: {ex.Message}");
                throw new Exception("Unable to send OTP email. Please try again.");
            }
        }
    }
}
