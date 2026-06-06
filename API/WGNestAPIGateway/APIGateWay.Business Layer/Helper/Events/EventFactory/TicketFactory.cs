using APIGateWay.ModalLayer.GETData;
using APIGateWay.ModalLayer.Helper;
using System;

namespace APIGateWay.Business_Layer.Helper.Events.EventFactory
{
    public static class TicketFactory
    {
        public static EventRequest TicketCreated(Guid issueId)
        {
            return new EventRequest
            {
                EventType = "TICKET_CREATED",
                EntityType = "TICKET",
                ConfigKey = "TicketsList",
                EntityId = issueId.ToString(),
                KeyField = "IssueId",
                MatchField = "Issue_Id",

                // --- TARGETING FIELDS ---
                AudienceField = "RepoId",           // Targets the Repo (Client)
                AssigneeField = "Assignee_Id",      // NEW: Targets the User (Employee Assignee)
                ResourceIdsField = "All_Assignees",
                // ------------------------

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

        // Accept a new 'changeSummary' parameter
        public static EventRequest TicketUpdated(Guid issueId, string changeSummary, bool notifyRepo, bool notifyUsers)
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
                AssigneeField = "Assignee_Id",
                ResourceIdsField = "All_Assignees",
                TitleField = "Title",
                CodeField = "Issue_Code",
                NotifyRepo = notifyRepo,
                NotifyUsers = notifyUsers,
                // 🔥 NEW: Inject the dynamic summary directly into the template
                MessageTemplate = $"Ticket {{Code}}: {changeSummary}",

                ResponseType = typeof(GetTickets),
                ContextMappings = { { "Role", "Role" } }
            };
        }
    }
}