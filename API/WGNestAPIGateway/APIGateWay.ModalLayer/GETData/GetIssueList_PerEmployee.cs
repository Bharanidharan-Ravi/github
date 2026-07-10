using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.GETData
{
    public class GetIssueList_PerEmployee
    {
      public string? EmployeeName { get; set; }
      public string? Title { get; set; }
      public int? Status { get; set; }
      public DateTime? UpdatedAt { get; set; }
        [Key]
      public Guid? UpdatedBy { get; set; }
      public Guid? Issue_Id { get; set; }
        
    }
}
