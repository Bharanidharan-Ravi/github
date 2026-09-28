using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APIGateWay.ModalLayer.MasterData
{
    // Maps 1:1 to dbo.LEAVE_REQUEST_DAY (scripts/leave_management_full_migration.sql).
    [Table("LEAVE_REQUEST_DAY")]
    public class LeaveRequestDayMaster
    {
        [Key]
        [Column("ID")]
        public Guid ID { get; set; }

        [Column("LEAVE_REQUEST_ID")]
        public Guid LEAVE_REQUEST_ID { get; set; }

        [Column("LEAVE_DATE")]
        public DateTime LEAVE_DATE { get; set; }

        [Column("DAY_SESSION")]
        [MaxLength(20)]
        public string DAY_SESSION { get; set; }
    }

    public static class LeaveDaySession
    {
        public const string Full = "FULL";
        public const string FirstHalf = "FIRST_HALF";
        public const string SecondHalf = "SECOND_HALF";

        public static readonly string[] All = { Full, FirstHalf, SecondHalf };

        // 1st half is 9:00 AM – 1:00 PM; once it's over only the 2nd half (2:00 PM – 6:30 PM) is left today.
        public static readonly TimeSpan FirstHalfEnd = new TimeSpan(13, 0, 0);

        public static decimal Days(string session) => session == Full ? 1m : 0.5m;

        public static string Label(string session) => session switch
        {
            FirstHalf => "1st half",
            SecondHalf => "2nd half",
            _ => "full day",
        };
    }
}
