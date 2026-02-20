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

    public class ProcessedAttachmentResult
    {
        // The HTML with /UploadsTemp/ replaced by /Uploads/
        public string UpdatedHtml { get; set; }

        // The entities ready to be added to the EF Core Context
        public List<AttachmentMaster> Attachments { get; set; } = new List<AttachmentMaster>();

        // Physical paths of the files created in the Permanent folder (used for Rollback)
        public List<string> PermanentFilePathsCreated { get; set; } = new List<string>();
    }
}
