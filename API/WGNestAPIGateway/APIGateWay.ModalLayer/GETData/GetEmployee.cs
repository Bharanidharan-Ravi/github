using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.GETData
{
    public class GetEmployee
    {
        public Guid UserID { get; set; }
        public string UserName { get; set; }
        public int? Team { get; set; }
        public int? Role { get; set; }
        public string? Specialization { get; set; }
        public string? Status { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public DateOnly? DoB { get; set; }

        public string? Attachment_JSON { get; set; }
        public string? LoginName { get; set; }

        [NotMapped]                   
        public string? PreviewUrl { get; set; }


    }
}


//anbu