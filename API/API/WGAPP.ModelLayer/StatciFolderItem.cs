using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WGAPP.ModelLayer
{
    public class StatciFolderItem
    {
        public string PhysicalPath { get; set; }
        public string RequestPath { get; set; }
    }

    public class SequenceResult
    {
        [Key]
        public int CurrentValue { get; set; }
    }
}
