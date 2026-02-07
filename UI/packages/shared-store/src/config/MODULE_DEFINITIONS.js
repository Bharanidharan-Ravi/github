import { GetAllReponew } from "../Thunks/RepoThunk";
import { RepoDefinition } from "./RepoDefinition";

export const MODULE_DEFINITIONS = {
    "repository":RepoDefinition,
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