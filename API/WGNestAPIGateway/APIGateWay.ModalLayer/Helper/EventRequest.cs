using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.Helper
{
    public class EventRequest
    {
        public string EventType { get; set; }

        public string EntityType { get; set; }

        public string ConfigKey { get; set; }

        public string EntityId { get; set; }

        public string KeyField { get; set; }

        public string MatchField { get; set; }

        public string AudienceField { get; set; }

        public string TitleField { get; set; }

        public string CodeField { get; set; }

        public string MessageTemplate { get; set; }

        public Type ResponseType { get; set; }

        public Dictionary<string, string> ContextMappings { get; set; }
            = new();
    }
}
