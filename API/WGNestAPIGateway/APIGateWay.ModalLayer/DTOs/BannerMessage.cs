using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.DTOs
{
    public class BannerMessageType
    {
        [Key]
        public Guid MessageTypeId { get; set; }
        public string? Type_Name { get; set; }
        public string? ColorCode { get; set; }
        public string? IconClass { get; set; }
    }
}
