using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.MasterData
{
    public class TeamMaster
    {
        [Key]
        public int TeamId { get; set; }
        public string TeamName { get; set; }
    }
}
