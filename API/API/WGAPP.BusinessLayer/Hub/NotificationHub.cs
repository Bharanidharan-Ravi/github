using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.SignalR;
using WGAPP.ModelLayer.GithubModal.TicketingModal;


namespace WGAPP.BusinessLayer.Hub
{
    [EnableCors("AllowAll")]
    [AllowAnonymous]
    public class NotificationHub : Microsoft.AspNetCore.SignalR.Hub   
    {
        public async Task JoinGroup(string groupName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        }

        public async Task LeaveGroup(string groupName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        }
    }
}
