using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.GETData
{
    public class GetThreadWorkType
    {
        [Key]
        public int TypeId {  get; set; }
        public string TypeName {  get; set; }
        //public string Support {  get; set; }
        //public string Discussion {  get; set; }
    }
}
