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
    public interface INotificationService
    {
        Task TicketCreated(GetAllIssueData ticket, int role);
        Task TicketUpdated(TicketHubModal ticket);
        Task TicketDeleted(int id);
        Task RepoCreated(RepoData ticket);
        Task RepoUpdated(TicketHubModal ticket);
        Task RepoDeleted(int id);
        
        Task ProjCreated(GetProject ticket);
        Task ProjUpdated(TicketHubModal ticket);
        Task ProjDeleted(int id);

    }
}