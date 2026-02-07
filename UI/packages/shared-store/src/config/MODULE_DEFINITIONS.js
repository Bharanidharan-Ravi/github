import { GetAllReponew } from "../Thunks/RepoThunk";
import { DashboardDefinition } from "./Dashboard.config";
import { ProjectDefinition } from "./Project.config";
import { RepoDefinition } from "./RepoDefinition";
import { TicketDefinition } from "./Ticket.config";

export const MODULE_DEFINITIONS = {
    "repository":RepoDefinition,
    "dashboard": DashboardDefinition,
    // "tickets": TicketDefinition,
    // "projects": ProjectDefinition
    // "tickets": {
    //     name: "Ticket",
    //     listKey: "tickets",
    //     initKey: "isTicketInitialized",
    //     fetcherName: "ensureTicketsLoaded",
    //     // api: GetAllTickets,
    //     idKey: "ticket_Id",
    //     eventPrefix: "Ticket",
    // },
    // Adding a new module is just adding JSON here!
};