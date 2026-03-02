using System;
using System.Collections.Generic;
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
        public string? HtmlDesc { get; set; }
        public TempReturn? temp { get; set; }
    }
}
