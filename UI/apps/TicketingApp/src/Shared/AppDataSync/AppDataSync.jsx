import { useSignalR } from "shared-signalr";
import { useDynamicSync } from "../../Hooks/useDynamicSync.js";

export const AppDataSync = () => {
    // 1. Start SignalR Global Connection
    // This persists across all page navigations
   useSignalR();
    console.log("usesignalr");
    

    // 2. Start Dynamic Route Listening
    // This hook now handles BOTH:
    //  - Connecting the right SignalR events (e.g., "RepoCreated")
    //  - Triggering the Background Fetch (e.g., ensureRepositoriesLoaded)
    useDynamicSync();

    // No Eager Load (Promise.all) here anymore.
    // Data fetching happens strictly when you visit the route.

    return null; // Headless component
};