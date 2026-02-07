import { GetAllReponew } from "../Thunks/RepoThunk";

export const MODULE_DEFINITIONS = {
    "repository": {
        data: {
        name: "repo",
        listKey: "repositories",
        initKey: "isRepoInitialized",
        fetcherName: "ensureRepoLoaded",
        api: GetAllReponew,
        idKey: "repo_Id",
        eventPrefix: "Repo", // Used for SignalR
        }
    },
    "tickets": {
        name: "Ticket",
        listKey: "tickets",
        initKey: "isTicketInitialized",
        fetcherName: "ensureTicketsLoaded",
        // api: GetAllTickets,
        idKey: "ticket_Id",
        eventPrefix: "Ticket",
    },
    // Adding a new module is just adding JSON here!
};