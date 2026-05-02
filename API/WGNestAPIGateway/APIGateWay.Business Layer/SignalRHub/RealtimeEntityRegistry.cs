using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.SignalRHub
{
    public static class RealtimeEntities
    {
        public static class Ticket
        {
            public const string Entity = "Ticket";
            public const string KeyField = "Issue_Id";
        }

        public static class ThreadsList
        {
            public const string Entity = "ThreadsList";
            public const string KeyField = "Thread_Id";
        }

        public static class TicketHistory
        {
            public const string Entity = "TicketHistory";
            public const string KeyField = "History_Id";
        }

        public static class Project
        {
            public const string Entity = "Project";
            public const string KeyField = "Id";
        }

        public static class Employee
        {
            public const string Entity = "Employee";
            public const string KeyField = "Employee_Id";
        }

        public static class Label
        {
            public const string Entity = "Label";
            public const string KeyField = "Label_Id";
        }

        public static class RepoList
        {
            public const string Entity = "RepoList";
            public const string KeyField = "Repo_Id";
        }

        // ── Future entities — add here when needed ────────────────────────
        // public static class DailyPlan
        // {
        //     public const string Entity   = "DailyPlan";
        //     public const string KeyField = "Plan_Id";
        // }

        // public static class Sprint
        // {
        //     public const string Entity   = "Sprint";
        //     public const string KeyField = "Sprint_Id";
        // }
    }

    public static class RealtimeActions
    {
        public const string Create = "Create";
        public const string Update = "Update";
        public const string Delete = "Delete";
    }
}
