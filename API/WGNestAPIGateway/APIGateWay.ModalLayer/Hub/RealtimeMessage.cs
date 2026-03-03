using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.Hub
{
    public class RealtimeMessage
    {
        public string Entity { get; set; }
        public string Action { get; set; }
        public string KeyField { get; set; }
        public object Payload { get; set; }
        public string? RepoKey { get; set; }
        public Guid? IssueId { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
