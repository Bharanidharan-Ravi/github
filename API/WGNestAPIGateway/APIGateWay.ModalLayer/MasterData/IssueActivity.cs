using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APIGateWay.ModalLayer.MasterData
{
    // One row per status/priority/remarks/hours change of an IssueLog entry
    // (the first row is written when the issue is created). An issue is
    // identified by Issue_Id (the ticket) + IssuelogId (its number within the
    // ticket); the latest row for it is the issue's current state.
    [Table("ISSUEACTIVITY")]
    public class IssueActivity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long ActivityId { get; set; }
        public Guid Issue_Id { get; set; }
        public int IssuelogId { get; set; }
        public long ThreadId { get; set; }
        public string? Priority { get; set; }
        public string? Status { get; set; }
        public string? Remarks { get; set; }
        public string? Hours { get; set; }

  
        public string? AttachmentsHtml { get; set; }

        public Guid ChangedBy { get; set; }

    }
}
