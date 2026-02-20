using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.PostData
{
    internal class AttachmentModal
    {
    }
    public class TempReturn
    {
        public string Delete { get; set; }
        public List<Tempdata> temps { get; set; }
    }

    public class Tempdata
    {
        public string FileName { get; set; }
        public string PublicUrl { get; set; }
        public string LocalPath { get; set; }
    }
}
