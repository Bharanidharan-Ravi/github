using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.PostData
{
    public class PolicyDto
    {
        public int Id { get; set; }
        public string FileName { get; set; }    
        public string FileUrl { get; set; }    
        public DateTime UpdatedAt { get; set; }    
        public string UpdatedBy { get; set; }    
        public string Attachment_JSON { get; set; }
        [NotMapped]
        public string? PreviewUrl { get; set; }

    }

    public class PostPolicyDto
    {
        public TempReturn temp { get; set; }
    }
}
