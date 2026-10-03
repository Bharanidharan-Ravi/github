using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.GETData
{
    public class GetAllUserOnlineStatus
    {
        [Key]
        public Guid EmployeeID { get; set; }
        public string EmployeeName { get; set; }

        public DateTime? LoginAt { get; set; }
        public DateTime? LogoutAt { get; set; }
        public DateTime? LastHeartbeat { get; set; }
        public bool? IsActive { get; set; }

        // Presence computed by the SP: online → since login, offline → since the
        // last session ended (null = never logged in)
        public bool IsOnline { get; set; }
        public DateTime? StatusSince { get; set; }
    }
}
