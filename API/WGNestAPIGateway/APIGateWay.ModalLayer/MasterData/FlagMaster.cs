using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.MasterData
{
    [Table("FlagMaster")]
    public class FlagMaster
    {
        public int Id { get; set; }
        public string FlagName { get; set; }
        public string? Description { get; set; }
        public string? Role { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid? UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
