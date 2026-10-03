using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.GETData
{
    public class GetHoliday
    {
        public int Id { get; set; } 
        public DateTime HolidayDate { get; set; } 
        public string Day { get; set; } 
        public string HolidayName { get; set; } 
        public DateTime CreatedAt { get; set; } 
        public Guid UpdatedBy { get; set; } 
    }
}
