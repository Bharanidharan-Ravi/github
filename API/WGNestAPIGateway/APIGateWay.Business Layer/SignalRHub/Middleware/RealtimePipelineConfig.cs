using APIGateWay.Business_Layer.SignalRHub;
using APIGateWay.Business_Layer.SignalRHub.Middleware;
using APIGateWay.ModalLayer.GETData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.BusinessLayer.SignalRHub.Middleware
{
    public static class RealtimePipelineConfig
    {
        public static void Configure(RealtimeBroadcastPipeline pipeline)
        {
            pipeline

            // ── Ticket ────────────────────────────────────────────────────────
            .Register<GetTickets>(
                method: "POST",
                routePattern: "/api/Ticket/CreateTicket",
                action: RealtimeActions.Create,
                config: RealtimeBroadcastRegistry.Ticket)

            .Register<GetTickets>(
                method: "PUT",
                routePattern: "/api/Ticket/{id}",
                action: RealtimeActions.Update,
                config: RealtimeBroadcastRegistry.Ticket)

            .Register<GetTickets>(
                method: "DELETE",
                routePattern: "/api/Ticket/DeleteTicket/{id}",
                action: RealtimeActions.Delete,
                config: RealtimeBroadcastRegistry.Ticket)

            // ── Thread (comments) ─────────────────────────────────────────────
            .Register<ThreadList>(
                method: "POST",
                routePattern: "/api/Thread/CreateThread",
                action: RealtimeActions.Create,
                config: RealtimeBroadcastRegistry.Thread)

            .Register<ThreadList>(
                method: "PUT",
                routePattern: "/api/Thread/UpdateThread/{id}",
                action: RealtimeActions.Update,
                config: RealtimeBroadcastRegistry.Thread)

            .Register<ThreadList>(
                method: "DELETE",
                routePattern: "/api/Thread/DeleteThread/{id}",
                action: RealtimeActions.Delete,
                config: RealtimeBroadcastRegistry.Thread)

            // ── Project ───────────────────────────────────────────────────────
            .Register<GetProject>(
                method: "POST",
                routePattern: "/api/Project/CreateProject",
                action: RealtimeActions.Create,
                config: RealtimeBroadcastRegistry.Project)

            .Register<GetProject>(
                method: "PUT",
                routePattern: "/api/Project/UpdateProject/{id}",
                action: RealtimeActions.Update,
                config: RealtimeBroadcastRegistry.Project)

            .Register<GetProject>(
                method: "DELETE",
                routePattern: "/api/Project/DeleteProject/{id}",
                action: RealtimeActions.Delete,
                config: RealtimeBroadcastRegistry.Project)

            // ── Employee ──────────────────────────────────────────────────────
            .Register<GetEmployee>(
                method: "POST",
                routePattern: "/api/Employee/CreateEmployee",
                action: RealtimeActions.Create,
                config: RealtimeBroadcastRegistry.Employee)

            .Register<GetEmployee>(
                method: "PUT",
                routePattern: "/api/Employee/UpdateEmployee/{id}",
                action: RealtimeActions.Update,
                config: RealtimeBroadcastRegistry.Employee)

            // ── Label ─────────────────────────────────────────────────────────
            .Register<GetLabel>(
                method: "POST",
                routePattern: "/api/Label/CreateLabel",
                action: RealtimeActions.Create,
                config: RealtimeBroadcastRegistry.Label)

            .Register<GetLabel>(
                method: "PUT",
                routePattern: "/api/Label/UpdateLabel/{id}",
                action: RealtimeActions.Update,
                config: RealtimeBroadcastRegistry.Label)

            .Register<GetLabel>(
                method: "DELETE",
                routePattern: "/api/Label/DeleteLabel/{id}",
                action: RealtimeActions.Delete,
                config: RealtimeBroadcastRegistry.Label)

            // ── Repo ──────────────────────────────────────────────────────────
            .Register<GetRepo>(
                method: "POST",
                routePattern: "/api/Repo/CreateRepo",
                action: RealtimeActions.Create,
                config: RealtimeBroadcastRegistry.Repo)

            .Register<GetRepo>(
                method: "PUT",
                routePattern: "/api/Repo/UpdateRepo/{id}",
                action: RealtimeActions.Update,
                config: RealtimeBroadcastRegistry.Repo);

            // ── Adding a new entity ───────────────────────────────────────────
            // Step 1: Add constants to RealtimeEntityRegistry.cs
            // Step 2: Add config to RealtimeBroadcastRegistry.cs
            // Step 3: Register routes below — done.
            //
            // .Register<GetDailyPlan>(
            //     method       : "POST",
            //     routePattern : "/api/DailyPlan/Create",
            //     action       : RealtimeActions.Create,
            //     config       : RealtimeBroadcastRegistry.DailyPlan)
        }
    }
}
