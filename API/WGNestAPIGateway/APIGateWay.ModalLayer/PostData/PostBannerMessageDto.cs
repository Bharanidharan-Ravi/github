using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.PostData
{
    public class PostBannerMessageDto
    {
        public string MessageText { get; set; }
        public Guid MessageTypeId { get; set; }
        public DateTime? StartDate { get;set; }
        public DateTime? EndDate { get; set; }
    }
    public class PutBannerMessageDto
    {
        [Key]
        public Guid BannerMessageId { get; set; }
        public string MessageText { get; set; }
        public Guid MessageTypeId { get; set; }
        public string Status { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
       
    }
}
