import { getConnection } from "./connection";

const registry = new Map();

function ensureSet(eventName) {
  if (!registry.has(eventName)) registry.set(eventName, new Set());
  return registry.get(eventName);
}

export function addEventHandler(eventName, handler) {
  const conn = getConnection();
  const set = ensureSet(eventName);
  // 1. Prevent Duplicates in our Registry
  if (set.has(handler)) return;
  set.add(handler);

  // // 2. Bind to SignalR if connected
  // if (conn) {
  //     // Safety: Off before On prevents double-binding in edge cases
  //     try { conn.off(eventName, handler); } catch (e) {}
  //     conn.on(eventName, handler);
  // }
  // // if (set.has(handler)) return;
  // // set.add(handler);

  if (conn) conn.on(eventName, handler);
}
export function removeEventHandler(eventName, handler) {
  // Fixed Typo
  const conn = getConnection();
  const set = registry.get(eventName);

  if (!set || !set.has(handler)) return;

  // 1. Remove from our Registry
  set.delete(handler);

  // 2. Detach from SignalR
  if (conn) {
    try {
      conn.off(eventName, handler);
    } catch (e) {
      console.warn("SignalR off failed", e);
    }
  }
}

export function removeAllHandlerForEvent(eventName) {
  const conn = getConnection();
  const set = registry.get(eventName);

  if (!set) return;

  // 1. Detach all handlers for this specific event
  for (const h of set) {
    try {
      if (conn) conn.off(eventName, h);
    } catch (e) {}
  }

  // 2. Clear registry entry
  registry.delete(eventName);
}

export function clearAllHandlers() {
  const conn = getConnection();

  // 1. Detach EVERYTHING
  for (const [eventName, set] of registry.entries()) {
    for (const h of set) {
      try {
        if (conn) conn.off(eventName, h);
      } catch (error) {}
    }
  }

  // 2. Clear Map AFTER the loop (Critical Fix)
  registry.clear();
}

export function reattachAllHandlers() {
    const conn = getConnection();
    if (!conn) return;
    for (const [eventName, set] of registry.entries()) {
        try {conn.on(eventName, h);}
         catch(e) {console.warn(`Failed to reattach ${eventName}`, e);
        }
    }
}
// export function reattachAllHandlers() {
//   const conn = getConnection();
//   if (!conn) return;

//   console.log("🔄 SignalR: Re-attaching all event handlers...");

//   // 1. Iterate over every Event Name
//   for (const [eventName, set] of registry.entries()) {
//     // 2. Iterate over every Handler Function in that Set
//     for (const handler of set) {
//       try {
//         // Safety: Ensure we don't bind twice
//         // conn.off(eventName, handler);
//         conn.on(eventName, handler);
//       } catch (e) {
//         console.warn(`Failed to reattach ${eventName}`, e);
//       }
//     }
//   }
// }
