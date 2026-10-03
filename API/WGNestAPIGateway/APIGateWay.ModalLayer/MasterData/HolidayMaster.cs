using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.MasterData
{
    [Table("HolidayMaster")]
    public class HolidayMaster
    {
        [Key]
        public int Id { get; set; }
        public DateTime HolidayDate { get; set; }
        public string Day { get; set; }
        public string HolidayName { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid UpdatedBy { get; set; }
    }
}
