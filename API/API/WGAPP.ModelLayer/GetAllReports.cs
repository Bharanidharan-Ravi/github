using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WGAPP.ModelLayer
{
    public class GetAllReports
    {
        [Key]
        public int ReportId { get; set; }
        public string QueryName { get; set; }
    }
}
