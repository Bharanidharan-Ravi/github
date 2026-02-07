using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WGAPP.ModelLayer.GithubModal.MasterData;
using WGAPP.ModelLayer.GithubModal.ViewIssues;

namespace WGAPP.ModelLayer.GithubModal.TicketingModal
{
    public class IssueMaster
    {
        [Key]
        public Guid? Issue_Id { get; set; }
        public int? SiNo { get; set; }
        public Guid? Repo_Id { get; set; }

        [MaxLength(500)]
        public string? Title { get; set; }

        public string? Description { get; set; }

        [MaxLength(500)]
        public Guid? Issuer_Id { get; set; }

        public DateTime? Created_On { get; set; }

   
        public DateTime? Updated_On { get; set; }

        public Guid? Project_Id { get; set; }

        [MaxLength(100)]
        public Guid? Assignee_Id { get; set; }

      
        public DateTime? Due_Date { get; set; }

        [MaxLength(100)]
        public string? Status { get; set; }

        
        public Guid? Issuelink_Id { get; set; }

        [MaxLength(100)]
        public string? Issue_Code { get; set; }
        public Guid? Updated_By { get; set; }

        // Navigation
       // public virtual ICollection<IssueAttachment> IssueAttachments { get; set; }
    }
    public class AttachmentMaster
    {
        [Key]
        public int AttachmentId { get; set; }   // Auto-incremented Attachment ID
        public Guid? TicketId { get; set; }       // Foreign Key to TicketMaster
        public string FileName { get; set; }    // Name of the file
        public string FilePath { get; set; }    // Path of the file
        public string FileType { get; set; }    // Type of the file (image, pdf, etc.)
        public long FileSize { get; set; }      // Size of the file
        public Guid UploadedBy { get; set; }  // User who uploaded the file
        public DateTime CreatedOn { get; set; } // Date the file was uploaded
        public string Status { get; set; }      // Status of the file (Active, Deleted)
        public string FileExtension { get; set; } // File extension (jpg, png, etc.)
        public string RelativePath { get; set; }
        public int ThreadId { get; set; }

    }

    public class ISSUE_LABELS
    {
        [Key]
        public int Label_Id { get; set; }

        public Guid? Issue_Id { get; set; }
    }



    public class IssueMasterDto
    {
        public Guid? Repo_Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public Guid? Issuer_Id { get; set; }
        public DateTime Created_On { get; set; }
        public DateTime? Updated_On { get; set; }
        public Guid? Project_Id { get; set; }
        public Guid? Assignee_Id { get; set; }
        public DateTime Due_Date { get; set; }
        public string? Status { get; set; }
        public Guid Issuelink_Id { get; set; }
        public string Issue_Code { get; set; }
        public Guid? Updated_By { get; set; }
        public List<ISSUE_LABELS> Labels { get; set; }
        public TempReturn TempReturns { get; set; }
        //public List<AttachmentDto> Attachments { get; set; }
    }



    //public class AttachmentDto
    //{
    //    public string Attachment_Title { get; set; }
    //    public byte[] Attachment_File { get; set; }
    //    public string Created_By { get; set; }
    //    public DateTime? Created_On { get; set; }
    //    public DateTime? Updated_On { get; set; }
    //    public string Updated_By { get; set; }
    //    public string Repo_Id { get; set; }
    //    public string LineId { get; set; }
    //}


   

    //public class IssueAttachment
    //{
    //    [Key, Column(Order = 0)]
    //    public Guid? Issue_Id { get; set; }

    //    [Key, Column(Order = 1)]
    //    public int Attachment_Id { get; set; }

    //    // Navigation Properties
    //    [ForeignKey("Issue_Id")]
    //    public virtual IssueMaster Issue { get; set; }

    //    [ForeignKey("Attachment_Id")]
    //    public virtual AttachmentMaster Attachment { get; set; }
    //}

}
