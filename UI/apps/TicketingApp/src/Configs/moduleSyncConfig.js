import { useCustomStore } from "shared-store";

export const SYNC_CONFIG = {
    "repository": {
        store: useCustomStore,
        eventPrefix: "Repo",
        fetcher: "ensureRepositoriesLoaded",
        actions: { add: "addRepo", update: "updateRepo", delete: "deleteRepo" }
    },
    "tickets": {
        store: useCustomStore,
        eventPrefix: "Ticket",
        fetcher: "ensureTicketsLoaded",
        actions: { add: "addTicket", update: "updateTicket", delete: "deleteTicket" }
    },
    "projects": {
        store: useCustomStore,
        eventPrefix: "Proj",
        fetcher: "ensureProjectsLoaded",     // 👈 NEW
        actions: { add: "addProject", update: "updateProject", delete: "deleteProject" }
    }
};