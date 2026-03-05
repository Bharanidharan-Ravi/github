using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.GETData
{
    public class GetLabel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Color { get; set; }
        public string? Status { get; set; }
        //public DateTime? Created_On { get; set; }
        //public string? Created_By { get; set; }
        //public DateTime? Updated_On { get; set; }
        //public string? Updated_By { get; set; }
    }
}
