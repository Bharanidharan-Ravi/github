using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static APIGateWay.ModalLayer.Helper.PostHelper;

namespace APIGateWay.ModalLayer.MasterData
{
    [Table("WorkStreamHandoffs")]
    public class WorkStreamHandoff : IAuditableEntity, IAuditableUser
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int HandsOffId { get; set; }

        public Guid IssueId { get; set; }

        // The Developer who wrote the code
        public Guid SourceStreamId { get; set; }

        // The Tester assigned to review it
        public Guid TargetStreamId { get; set; }

        // The thread/comment the developer posted when moving it to QA
        public long InitiatingThreadId { get; set; }

        // Status: "Pending", "Passed", "Failed"
        [MaxLength(20)]
        public string Status { get; set; } = "Pending";

        // SELF-REFERENCING: If this handoff fails, which future handoff fixes it?
        public int? ResolvedByHandoffId { get; set; }

        public DateTime? CreatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // ─── NAVIGATION PROPERTIES (For EF Core) ─────────────────────

        // 1. Link back to the parent WorkStreams
        [ForeignKey("SourceStreamId")]
        public virtual WorkStream SourceStream { get; set; }

        [ForeignKey("TargetStreamId")]
        public virtual WorkStream TargetStream { get; set; }

        // 2. Link to the specific thread that started this handoff
        [ForeignKey("InitiatingThreadId")]
        public virtual ThreadMaster InitiatingThread { get; set; }

        // 3. Link to the BUGS! (1 Handoff can have MANY bug threads)
        // This maps to the FailedHandoffId column you will add to ISSUETHREADS
        [InverseProperty("FailedHandoff")] // 🔥 ADD THIS LINE
        public virtual ICollection<ThreadMaster> BugThreads { get; set; }
    }

    public static class HandoffStatus
    {
        public const string Pending = "Pending";   // Pushed, awaiting review
        public const string Failed = "Failed";    // Bug reported by tester
        public const string Passed = "Passed";    // Test cleared
        public const string Recalled = "Recalled";  // Source retracted before test
    }
}
