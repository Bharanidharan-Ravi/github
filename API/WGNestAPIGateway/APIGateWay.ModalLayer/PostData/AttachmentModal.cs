using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static APIGateWay.ModalLayer.Helper.PostHelper;

namespace APIGateWay.ModalLayer.PostData
{
    public class AttachmentMaster : IAuditableEntity, IAuditableUser
    {
        [Key]
        public int AttachmentId { get; set; }
        public string? ModuleId { get; set; } 
        public string FileName { get; set; } 
        public string FilePath { get; set; } 
        public string FileType { get; set; } 
        public long FileSize { get; set; } 
        public Guid? CreatedBy { get; set; } 
        public DateTime? CreatedAt { get; set; }
        public Guid? UpdatedBy { get; set; }
        public  DateTime? UpdatedAt { get; set; } 
        public string Status { get; set; } 
        public string FileExtension { get; set; } 
        public string RelativePath { get; set; } 
        public string Module { get; set; } 
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
