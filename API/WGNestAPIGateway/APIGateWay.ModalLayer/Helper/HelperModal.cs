using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.Helper
{
    public class HelperModal
    {
        public class SequenceResult
        {
            [Key]
            public int CurrentValue { get; set; }
            public int? ColumnValue { get; set; }
        }

        public class StaticFolderItem
        {
            public string PhysicalPath { get; set; }
            public string RequestPath { get; set; }
        }
    }
    
}
