using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.PostData
{
    public static class StatusId
    {
        // ── WorkStream / Subtask statuses ─────────────────────────────────────
        public const int New = 1;   // NEW
        public const int Assigned = 2;   // ASS
        public const int InAnalysis = 3;   // ANA
        public const int AwaitingInformation = 4;   // INFO
        public const int InDevelopment = 5;   // DEV
        public const int DevelopmentCompleted = 6;   // DEV-C
        public const int UnitTesting = 7;   // UTEST
        public const int FunctionalTesting = 8;   // FTEST
        //public const int QATesting = 9;   // QA
        public const int UATTesting = 9;  // UAT
        public const int AwaitingClientResponse = 10;  // ACR
        public const int FunctionalFixCompleted = 11;  // FUNC-C
        //public const int TransportCreated = 13;  // TR-C
        //public const int TransportReleased = 14;  // TR-R
        //public const int MovedToQA = 15;  // QA-M
        public const int MovedToProduction = 12;  // PRD-M
        public const int OnHold = 13;  // HLD
        public const int Closed = 14;  // CLS
        public const int Cancelled = 15;  // CAN
        public const int Inactive = 16;  // INA

        // ── Helpers ───────────────────────────────────────────────────────────

        // Statuses that mean "this subtask is still active / not done"
        public static readonly HashSet<int> ActiveStatuses = new()
        {
            New, Assigned, InAnalysis, AwaitingInformation,
            InDevelopment, UnitTesting, FunctionalTesting,
            //QATesting,
            UATTesting, AwaitingClientResponse, OnHold
        };

        // Statuses that count as "completed" for ticket auto-completion check
        // ALL non-inactive subtasks must be in one of these for ticket to close
        public static readonly HashSet<int> CompletedStatuses = new()
        {
            DevelopmentCompleted, FunctionalFixCompleted,
            //TransportCreated, TransportReleased, MovedToQA, 
            MovedToProduction,
            Closed
        };

        // Statuses that mean "removed / hidden" — never count toward completion
        public static readonly HashSet<int> InactiveStatuses = new()
        {
            Inactive, Cancelled
        };

        public static bool IsActive(int statusId) => ActiveStatuses.Contains(statusId);
        public static bool IsCompleted(int statusId) => CompletedStatuses.Contains(statusId);
        public static bool IsInactive(int statusId) => InactiveStatuses.Contains(statusId);

        // Dev family: InDevelopment and its completion state
        public static readonly HashSet<int> DevFamily = new()
        {
            InDevelopment,        // 5
            DevelopmentCompleted, // 6
        };

                // Testing family: all testing stages and their completion states
        public static readonly HashSet<int> TestingFamily = new()
        {
            UnitTesting,           // 7
            FunctionalTesting,     // 8
            UATTesting,            // 9
            AwaitingClientResponse,// 10
            FunctionalFixCompleted,// 11
        };

                // Production family
         public static readonly HashSet<int> ProductionFamily = new()
        {
            MovedToProduction,     // 12
        };

                // Terminal family
        public static readonly HashSet<int> TerminalFamily = new()
        {
            Closed,     // 14
            Cancelled,  // 15
        };

        // Helper: get which family a status belongs to
        public static HashSet<int>? GetFamily(int statusId)
        {
            if (DevFamily.Contains(statusId)) return DevFamily;
            if (TestingFamily.Contains(statusId)) return TestingFamily;
            if (ProductionFamily.Contains(statusId)) return ProductionFamily;
            return null;
        }

        // Helper: are two statuses in the same family?
        public static bool SameFamily(int a, int b)
        {
            var family = GetFamily(a);
            return family != null && family.Contains(b);
        }
    }
}
