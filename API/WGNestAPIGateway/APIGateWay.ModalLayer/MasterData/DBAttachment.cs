using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.MasterData
{
    public class DBAttachment
    {
        [Key]
        public int AttachmentID { get; set; }

        public string FileName { get; set; }

        public string ContentType { get; set; }

        public byte[] FileData { get; set; }
    }
}
