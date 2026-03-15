using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.MasterData
{
    public class StatusMaster
    {
        [Key]
        public int Status_Id { get; set; }
        public string Status_Code { get; set; }
        public string Status_Name { get; set; }
        public string Description { get; set; }
        public bool Is_Active { get; set; }
        public int Sort_Order { get; set; }
        //public string CreatedBy { get; set; }
        //public DateTime Created_At { get; set; }
        //public string? UpdatedBy { get; set; }
        //public DateTime? Updated_At { get; set; }
    }
}
