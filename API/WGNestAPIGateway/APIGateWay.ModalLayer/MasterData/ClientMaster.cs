using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.MasterData
{
    [Table("CLIENTS")]

    public class ClientMaster
    {
        [Key]
        public Guid Client_Id { get; set; }

        public string Client_Code { get; set; }
        public string Client_Name { get; set; }
        public string Description { get; set; }

        public string Created_By { get; set; }

        // Set default to now
        public DateTime Created_On { get; set; } = DateTime.Now;

        public string Updated_By { get; set; }

        // Make nullable to avoid sending 0001-01-01
        public DateTime? Updated_On { get; set; }

        // Dates sent from payload
        public DateTime? Valid_From { get; set; }
        public DateTime? Valid_To { get; set; }

        public string Status { get; set; } = "Active";
    }
}