using APIGateWay.ModalLayer.GETData;
using APIGateWay.ModalLayer.Helper;
using APIGateWay.ModalLayer.MasterData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Helpers
{
    public static class TicketFactory
    {
        public static EventRequest TicketCreated(
            Guid issueId)
        {
            return new EventRequest
            {
                EventType = "TICKET_CREATED",

                EntityType = "TICKET",

                ConfigKey = "TicketsList",

                EntityId = issueId.ToString(),

                KeyField = "IssueId",

                MatchField = "Issue_Id",

                AudienceField = "RepoId",

                TitleField = "Title",

                CodeField = "Issue_Code",

                MessageTemplate = "Ticket {Code} Created",

                ResponseType = typeof(GetTickets),

                ContextMappings =
            {
                { "Role", "Role" }
            }
            };
        }

        public static EventRequest TicketUpdated(
            Guid issueId)
        {
            return new EventRequest
            {
                EventType = "TICKET_UPDATED",

                EntityType = "TICKET",

                ConfigKey = "TicketsList",

                EntityId = issueId.ToString(),

                KeyField = "IssueId",

                MatchField = "Issue_Id",

                AudienceField = "RepoId",

                TitleField = "Title",

                CodeField = "Issue_Code",

                MessageTemplate = "Ticket {Code} Updated",

                ResponseType = typeof(GetTickets),

                ContextMappings =
            {
                { "Role", "Role" }
            }
            };
        }
    }
}
