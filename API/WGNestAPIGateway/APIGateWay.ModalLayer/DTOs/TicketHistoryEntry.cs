using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.DTOs
{
    public class TicketHistoryEntry
    {
        public Guid IssueId { get; set; }
        public string EventType { get; set; } = string.Empty;  // HistoryEventType.XXX
        public string Summary { get; set; } = string.Empty;
        public string? FieldName { get; set; }
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public Guid ActorId { get; set; }
        public string ActorName { get; set; } = string.Empty;
        public Guid? WorkStreamId { get; set; }
        public long? ThreadId { get; set; }
        public string? TargetEntityId { get; set; }
        public string? TargetEntityType { get; set; }
        public object? Meta { get; set; }  // serialized to MetaJson
    }
}