using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WGAPP.ModelLayer.GithubModal.ProjectModal;
using WGAPP.ModelLayer.GithubModal.RepositoryModal;
using WGAPP.ModelLayer.GithubModal.TicketingModal;

namespace WGAPP.BusinessLayer.Hub
{
    public class NotificationService : INotificationService
    {
        private readonly IHubContext<NotificationHub> _hubContext;

        public NotificationService(IHubContext<NotificationHub> hubContext)
        {
            _hubContext = hubContext;
        }

        //// ticket created
        public async Task TicketCreated(GetAllIssueData ticket, int role)
        {
            // Determine dynamic method name based on role
            string methodName = role switch
            {
                3 => "ClientTicketCreated",       
                1 or 2 => "EmployeeTicketCreated", 
                _ => null 
            };

            // Broadcast to the group "tickets" with dynamic method name
            await _hubContext.Clients.Group("tickets").SendAsync(methodName, ticket);
        }

        public async Task TicketUpdated(TicketHubModal ticket)
        {
            await _hubContext.Clients.Group("tickets").SendAsync("TicketUpdated", ticket);
        }
        public async Task TicketDeleted(int id)
        {
            await _hubContext.Clients.Group("tickets").SendAsync("TicketDeleted", id);
        }
        public async Task RepoCreated(RepoData ticket)
        {
            await _hubContext.Clients.Group("tickets").SendAsync("RepoCreated", ticket);
        }
        public async Task RepoUpdated(TicketHubModal ticket)
        {
            await _hubContext.Clients.Group("repo").SendAsync("RepoUpdated", ticket);
        }
        public async Task RepoDeleted(int id)
        {
            await _hubContext.Clients.Group("repo").SendAsync("RepoDeleted", id);
        }

        #region project signalr
        public async Task ProjCreated(GetProject ticket)
        {
            await _hubContext.Clients.Group("tickets").SendAsync("ProjCreated", ticket);
        }
        public async Task ProjUpdated(TicketHubModal ticket)
        {
            await _hubContext.Clients.Group("repo").SendAsync("ProjUpdated", ticket);
        }
        public async Task ProjDeleted(int id)
        {
            await _hubContext.Clients.Group("repo").SendAsync("ProjDeleted", id);
        }
        #endregion
    }
}
