using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.PostData
{
    public class IssueLabel
    {
        [Key]
        public Guid? Issue_Id { get; set; }
        public int? Label_Id { get; set; }
    }
}
