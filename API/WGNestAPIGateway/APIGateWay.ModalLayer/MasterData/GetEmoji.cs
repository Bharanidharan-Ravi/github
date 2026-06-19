using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.MasterData
{
    public class Emoji_Reactions
    {
        public int? Id { get; set; }
        public int? ThreadId { get; set; }
        public string? Emoji { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTime? CreatedAt { get; set; }

    }
}
