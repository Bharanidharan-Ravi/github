import { safeInvoke } from "./connection";
const activeGroups = new Set();

export async function joinGroup(groupName) {
    if (!groupName) return;

    if(activeGroups.has(groupName)) return;
    try {
        await safeInvoke("JoinGroup", groupName);
        activeGroups.add(groupName);
        console.info("Signal: joined group", groupName);
    } catch (err) {
        console.warn("Signal: joinGroup failed", groupName, err);
        throw err;
    }
}

export async function leaveGroup(groupName) {
    if (!groupName) return;

    if(activeGroups.has(groupName)) return;
    try {
        await safeInvoke("leaveGroup", groupName);
        activeGroups.delete(groupName);
        console.info("SignalR: left group", groupName);
    } catch (err) {
        console.warn("SignalR: leaveGroup failed", groupName, err);
    }
}

export function getActiveGroups() {
    return [...activeGroups];
}