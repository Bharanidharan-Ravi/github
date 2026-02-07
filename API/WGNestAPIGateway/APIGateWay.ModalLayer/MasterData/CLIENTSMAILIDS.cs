using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.MasterData
{
    public class CLIENTSMAILIDS
    {
        public Guid? Client_Id { get; set; }
        [Key]
        public string MailIds { get; set; }
    }
}
