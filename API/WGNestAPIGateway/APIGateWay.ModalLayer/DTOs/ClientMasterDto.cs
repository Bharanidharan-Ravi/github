using APIGateWay.ModalLayer.MasterData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.DTOs
{
    public class ClientMasterDto
    {
        public string ClientCode { get; set; }
        public string ClientName { get; set; }
        public string Description { get; set; }
        public DateTime? Valid_From { get; set; } = DateTime.Today;
        public DateTime? Valid_To { get; set; }
        public List<CLIENTSMAILIDS> CLIENTSMAILIDS { get; set; }
    }
}
