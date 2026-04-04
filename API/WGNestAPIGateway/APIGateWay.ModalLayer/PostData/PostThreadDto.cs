using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.PostData
{
    public class PostThreadsDto
    {
        public string? CommentText { get; set; }
        public Guid Issue_Id { get; set; }
        public DateTime? From_Time { get; set; }
        public DateTime? To_Time { get; set; }
        public string? Hours { get; set; }
        public string StreamName { get; set; } = string.Empty;
        public decimal? CompletionPct { get; set; } = 0;
        public DateTime? TargetDate { get; set; }
        public Guid? ResourceId   { get; set; }

        // 1=InProgress 2=Hold 3=AwaitingClient — defaults to 1 if not sent
        public int? StreamStatus { get; set; }
        public string? HtmlDesc { get; set; }
        public TempReturn? temp { get; set; }
    }

    public class UpdateThreadDto
    {
        // Text Content
        public string? CommentText { get; set; }

        // Time Tracking
        public DateTime? From_Time { get; set; }
        public DateTime? To_Time { get; set; }
        public string? Hours { get; set; }

        // Workstream / Assignee Tracking
        // Note: Adjust Guid? to string? if your ResourceId is string-based
        public Guid? ResourceId { get; set; }
        public int? StreamStatus { get; set; } // Assumes StatusId maps to an int
        public int? CompletionPct { get; set; }
        public DateTime? TargetDate { get; set; }

        // Attachments
        public TempReturn? temp { get; set; }
    }
}