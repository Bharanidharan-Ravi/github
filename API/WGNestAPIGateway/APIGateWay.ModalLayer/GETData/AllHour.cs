using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.GETData
{
    public class AllHour
    {
        [Key]
        public Guid? Issue_Id { get; set; }
        public string? EstimatedHours { get; set; }
        public string? LoggedBreakdown { get; set; }
        public string? MyLoggedHours { get; set; }
    }
}
