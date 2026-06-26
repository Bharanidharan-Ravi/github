using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.PostData
{
    public class PostEmoji
    {
        public long ThreadId {  get; set; }
        public string Emoji {  get; set; }
        public Guid IssueId {  get; set; }
    }
}
