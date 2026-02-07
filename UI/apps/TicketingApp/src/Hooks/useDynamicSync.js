import { useEffect } from "react";
import { useLocation } from "react-router-dom";
import { useEvent } from "shared-signalr";
import { useCustomStore, MODULE_DEFINITIONS } from "shared-store";

export function useDynamicSync() {
    const location = useLocation();
    const pathSegments = location.pathname.toLowerCase().split("/");
    
    // 1. Identify Active Module
    const activeKey = Object.keys(MODULE_DEFINITIONS).find(key => pathSegments.includes(key));
    const config = activeKey ? MODULE_DEFINITIONS[activeKey].data : null;

    // 2. AUTO-SYNC LOGIC (Now with Dependencies!)
    useEffect(() => {
        if (config) {
            console.log(`🚀 Route [${activeKey}]: Syncing Main Data...`);
            
            // A. Fetch the Main Module (e.g., Tickets)
            const mainFetcher = useCustomStore.getState()[config.fetcherName];
            if (mainFetcher) mainFetcher();

            // B. Fetch Dependencies (e.g., Repos, Projects)
            if (config.dependencies && Array.isArray(config.dependencies)) {
                config.dependencies.forEach(depKey => {
                    const depDef = MODULE_DEFINITIONS[depKey];
                    if (depDef && depDef.data.fetcherName) {
                        console.log(`🔗 [${activeKey}] Loading Dependency: ${depKey}`);
                        
                        const depFetcher = useCustomStore.getState()[depDef.data.fetcherName];
                        if (depFetcher) depFetcher();
                    }
                });
            }
        }
    }, [activeKey]);

    // 3. SignalR Handlers
    const handleCreate = (data) => config && useCustomStore.getState()[`add${config.name}`](data);
    const handleUpdate = (data) => config && useCustomStore.getState()[`update${config.name}`](data);
    const handleDelete = (id) => config && useCustomStore.getState()[`delete${config.name}`](id);

    // 4. Listeners
    const prefix = config ? config.eventPrefix : "___Ignore";
    useEvent(`${prefix}Created`, handleCreate);
    useEvent(`${prefix}Updated`, handleUpdate);
    useEvent(`${prefix}Deleted`, handleDelete);
}


// useEffect(() => {
//     if (config) {
//         console.log(`🚀 Route [${activeKey}]: Syncing Data...`);

//         // 1. Create a UNIQUE set of fetchers to run
//         const fetchersToRun = new Set();

//         // Add Main Module (e.g., Tickets)
//         if (config.fetcherName) fetchersToRun.add(config.fetcherName);

//         // Add Dependencies (e.g., Repos, Projects)
//         if (config.dependencies) {
//             config.dependencies.forEach(depKey => {
//                 const depDef = MODULE_DEFINITIONS[depKey];
//                 if (depDef && depDef.data.fetcherName) {
//                     fetchersToRun.add(depDef.data.fetcherName);
//                 }
//             });
//         }

//         // 2. Execute them (The Store's "isInitialized" check handles the rest)
//         fetchersToRun.forEach(fnName => {
//             const fetcher = useCustomStore.getState()[fnName];
//             if (fetcher) {
//                 // This call is safe because createGenericSlice checks isInitialized!
//                 fetcher(); 
//             }
//         });
//     }
// }, [activeKey]);


/////-------------------------------------------------------------------------
// OLD CODE FOR REFERENCE
//--------------------------------------------------------------

// // apps/TicketingApp/src/hooks/useDynamicSync.js
// import { useEffect } from "react"; // Added useEffect
// import { useLocation } from "react-router-dom";
// import { useEvent } from "shared-signalr";
// import { SYNC_CONFIG } from "../Configs/moduleSyncConfig.js";
// import { useCustomStore } from "shared-store";


// export function useDynamicSync() {
//     const location = useLocation();
//     const pathSegments = location.pathname.toLowerCase().split("/");
    
//     // 1. Determine Active Module
//     const activeKey = Object.keys(SYNC_CONFIG).find(key => pathSegments.includes(key));
//     const config = activeKey ? SYNC_CONFIG[activeKey] : null;

//     // ---------------------------------------------------------
//     // 🚀 NEW: AUTOMATIC FETCHING (Route-Driven Sync)
//     // ---------------------------------------------------------
//     useEffect(() => {
//         if (config && config.fetcher) {
//             console.log(`🔄 Route Changed to [${activeKey}]: Triggering Background Sync...`);
//             const fetcher = config.fetcher;
//             console.log(`Fetcher: ${fetcher}`);
            
//             // Call the store action dynamically
//             // e.g., store.getState().ensureTicketsLoaded()
//             useCustomStore.getState()[fetcher]();
//         }
//     }, [activeKey]); // Only runs when switching modules (e.g., Repo -> Tickets)

//     // ---------------------------------------------------------
//     // SignalR Handlers (Existing Logic)
//     // ---------------------------------------------------------
//    const handleCreate = (data) => config && config.store.getState()[config.actions.add](data);
//     // const handleUpdate = (data) => config && config.store.getState()[config.actions.update](data);
//     // const handleDelete = (id) => config && config.store.getState()[config.actions.delete](id);

//     const prefix = config ? config.eventPrefix : "___Ignore";

//     useEvent(`${prefix}Created`, handleCreate);
//     // useEvent(`${prefix}Updated`, handleUpdate);
//     // useEvent(`${prefix}Deleted`, handleDelete);
// }