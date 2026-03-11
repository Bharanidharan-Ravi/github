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
}