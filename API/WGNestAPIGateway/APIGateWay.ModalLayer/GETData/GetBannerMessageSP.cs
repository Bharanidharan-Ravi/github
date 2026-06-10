using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.GETData
{
    public class GetBannerMessageSP
    {
        [Key]
        public Guid BannerMessageId { get; set; }
        public string MessageText { get; set; }
        public Guid MessageTypeId { get; set; }
        public string Status { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public Guid CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid? UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? Type_Name { get; set; }
        public string? ColorCode { get; set; }
        public string? IconClass { get; set; }
    }
}
