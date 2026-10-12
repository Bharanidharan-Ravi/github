using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net;
using System.Net.Mail;
using APIGateWay.BusinessLayer.Helper.Events.Interface;
using APIGateWay.DomainLayer.DBContext;
using APIGateWay.ModalLayer.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using APIGateWay.BusinessLayer.Interface;

namespace APIGateWay.BusinessLayer.Services
{
    public class ClientMailNotifier : IClientMailNotifier
    {
        private readonly APIGatewayDBContext _db;
        private readonly IConfiguration _cfg;

        public ClientMailNotifier(APIGatewayDBContext db, IConfiguration cfg)
        {
            _db = db;
            _cfg = cfg;
        }

        public async Task NotifyThreadToClientAsync(Guid issueId, string threadHtml, string postedBy, bool isUpdate)
        {
            try
            {
                var ticket = await _db.Set<TicketMaster>().AsNoTracking()
                    .Where(t => t.Issue_Id == issueId)
                    .Select(t => new { t.Issue_Code, t.Title, t.RepoId })
                    .FirstOrDefaultAsync();
                if (ticket?.RepoId == null) return;

                var repoKey = await _db.RepositoryMasters
                    .Where(r => r.Repo_Id == ticket.RepoId)
                    .Select(r => r.RepoKey).FirstOrDefaultAsync();
                if (string.IsNullOrEmpty(repoKey)) return;

                var emails = await _db.RepoUsers
                    .Where(u => u.RepoKey == repoKey && u.Status == "Active" && u.MailId != null)
                    .Select(u => u.MailId).Distinct().ToListAsync();
                if (emails.Count == 0) return;

                var s = _cfg.GetSection("Smtp");
                var action = isUpdate ? "updated" : "posted";

                using var msg = new MailMessage
                {
                    From = new MailAddress(s["From"]!),
                    IsBodyHtml = true,
                    Subject = $"[{ticket.Issue_Code}] {ticket.Title} - thread {action}",
                    Body = $"<p><b>{postedBy}</b> {action} a thread on ticket <b>{ticket.Issue_Code} - {ticket.Title}</b>.</p>{threadHtml}"
                };
                foreach (var e in emails) msg.Bcc.Add(e);   // BCC: clients don't see each other

                using var smtp = new SmtpClient(s["Host"], int.Parse(s["Port"]!))
                {
                    EnableSsl = true,
                    Credentials = new NetworkCredential(s["User"], s["Password"])
                };
                await smtp.SendMailAsync(msg);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ClientMail] failed: {ex.Message}");   // never break the API
            }
        }
    }
}


