using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

[Table("MeetingMaster")]
public class MeetingMaster
{
    [Key]
    public Guid meeting_id { get; set; }

    [Required]
    [StringLength(50)]
    public string host_type { get; set; }

    [Required]
    public Guid host_id { get; set; }

    [Required]
    [StringLength(255)]
    public string title { get; set; }

    [StringLength(50)]
    public string? meet_method { get; set; }

    [StringLength(500)]
    public string? meet_link { get; set; }

    [StringLength(100)]
    public string? meet_password { get; set; }

    public Guid? ticket_id { get; set; }

    public long? ThreadId { get; set; }

    public Guid? project_id { get; set; }

    public DateTime? valid_from_date { get; set; }

    public DateTime? valid_to_date { get; set; }

    // Mapped from TIME(0)
    public string? start_time { get; set; }

    // Mapped from TIME(0)
    public string? end_time { get; set; }

    [StringLength(100)]
    public string? time_zone_id { get; set; }

    [StringLength(7)]
    public string? days_of_week { get; set; }

    [StringLength(50)]
    public string recurrence_type { get; set; }

    // Mapped from INT (minutes)
    public string? slot_duration { get; set; }

    [StringLength(50)]
    public string booking_type { get; set; }

    [StringLength(30)]
    public string status { get; set; }

    public string? meeting_summary { get; set; }

    public Guid? created_by { get; set; }

    public Guid? updated_by { get; set; }

    public DateTime? created_at { get; set; }

    public DateTime? updated_at { get; set; }

    public DateTime? meeting_date { get; set; }
}


