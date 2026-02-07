using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WGAPP.ModelLayer.GithubModal.TicketingModal
{
    public class TicketHubModal
    {
        [Key]
        public Guid? Id { get; set; }
        public string Title { get; set; }
       

    }
}
