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

        public static EventRequest MeetingCompleted(Guid issueId, string summary, bool notifyRepo, bool notifyUsers)
        {
            return new EventRequest
            {
                EventType = "MEETING_COMPLETED",

                EntityType = "THREAD",

                ConfigKey = "ThreadList",

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

                MessageTemplate = $"Meeting completed : {summary}",

                ResponseType = typeof(ThreadList),

                ContextMappings =
                {
                    { "Role","Role" }
                }
            };
        }

        public static EventRequest ThreadCreated(Guid issueId, long threadId)
        {
            return new EventRequest
            {
                EntityId = issueId.ToString(), // Used to populate @IssuesId for the SP
                ThreadId = threadId,

                EventType = "Create",
                EntityType = "Thread",
                ConfigKey = "ThreadsList",

                KeyField = "IssuesId",  // DB PARAM: Matches the parameter in your SP
                MatchField = "ThreadId",// C# FILTER: Matches the property in ThreadList class

                AudienceField = "RepoId",
                AssigneeField = "Assignee_Id",
                ResourceIdsField = "All_Assignees",

                // Prevent ArgumentNullException by mapping these required fields
                TitleField = "ThreadId",
                CodeField = "Issue_Id",
                MessageTemplate = "New meeting thread posted on Ticket {Code}",

                NotifyRepo = true,
                NotifyUsers = true,
                ResponseType = typeof(ThreadList),

                ContextMappings =
                {
                    { "Role", "Role" } // Passes @Role to the SP
                }
            };
        }

        public static EventRequest ThreadUpdated(Guid issueId, long threadId)
        {
            return new EventRequest
            {
                EntityId = issueId.ToString(), // Used for @IssuesId in SP
                ThreadId = threadId,           // Used for C# filtering

                EventType = "Update",
                EntityType = "Thread",
                ConfigKey = "ThreadsList",

                KeyField = "IssuesId",
                MatchField = "ThreadId",       // 🌟 FIX 1: Filter by ThreadId, not Issue_Id

                // 🌟 FIX 2: Add routing and notification fields so it doesn't crash/drop
                AudienceField = "RepoId",
                AssigneeField = "Assignee_Id",
                ResourceIdsField = "All_Assignees",
                TitleField = "ThreadId",
                CodeField = "Issue_Id",

                ResponseType = typeof(ThreadList),

                NotifyRepo = true,
                NotifyUsers = true,

                MessageTemplate = "Meeting details updated on Ticket {Code}",

                // 🌟 FIX 3: Add ContextMappings so the SP gets the @Role parameter
                ContextMappings =
                {
                    { "Role", "Role" }
                }
            };
        }
    }
}