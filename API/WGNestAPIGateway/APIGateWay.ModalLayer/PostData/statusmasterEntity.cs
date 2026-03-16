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
        public const int QATesting = 9;   // QA
        public const int UATTesting = 10;  // UAT
        public const int AwaitingClientResponse = 11;  // ACR
        public const int FunctionalFixCompleted = 12;  // FUNC-C
        public const int TransportCreated = 13;  // TR-C
        public const int TransportReleased = 14;  // TR-R
        public const int MovedToQA = 15;  // QA-M
        public const int MovedToProduction = 16;  // PRD-M
        public const int OnHold = 17;  // HLD
        public const int Closed = 18;  // CLS
        public const int Cancelled = 19;  // CAN
        public const int Inactive = 20;  // INA

        // ── Helpers ───────────────────────────────────────────────────────────

        // Statuses that mean "this subtask is still active / not done"
        public static readonly HashSet<int> ActiveStatuses = new()
        {
            New, Assigned, InAnalysis, AwaitingInformation,
            InDevelopment, UnitTesting, FunctionalTesting,
            QATesting, UATTesting, AwaitingClientResponse, OnHold
        };

        // Statuses that count as "completed" for ticket auto-completion check
        // ALL non-inactive subtasks must be in one of these for ticket to close
        public static readonly HashSet<int> CompletedStatuses = new()
        {
            DevelopmentCompleted, FunctionalFixCompleted,
            TransportCreated, TransportReleased,
            MovedToQA, MovedToProduction,
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
    }
}
