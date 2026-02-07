using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WGAPP.ModelLayer.GithubModal.TicketingModal
{
    public class GetAllIssueData
    {
        [Key]
        public Guid Issue_Id { get; set; }
        public string? Issue_Title { get; set; }
        public string? Description { get; set; }
        public Guid? Issuer_Id { get; set; }
        public string? Issuer_Name { get; set; }
        public DateTime Created_On { get; set; }
        public Guid? Updated_By { get; set; }
        public DateTime? Updated_On { get; set; }
        public Guid Project_Id { get; set; }
        public Guid? Assignee_Id { get; set; }
        public string? Assignee_Name { get; set; }
        public DateTime? Due_Date { get; set; }
        public string? Status { get; set; }
        public string? Issue_Code { get; set; }
        public List<GETLABELFORISSUES> Labels_JSON { get; set; }
        public List<GETATTACHFORISSUES> Attachment_JSON { get; set; }
    }

    public class GETLABELFORISSUES
    {
        [Key]
        public int LABEL_ID { get; set; }
        public string LABEL_TITLE { get; set; }
        public string LABEL_COLOR { get; set; }
    }
    public class GETATTACHFORISSUES
    {
        [Key]
        public int AttachmentId { get; set; }
        public string FileName { get; set; }
        public string PublicUrl { get; set; }
        public string RelativePath { get; set; }
    }
}
