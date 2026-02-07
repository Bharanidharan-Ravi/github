import * as signalR from '@microsoft/signalr';
import { useCustomStore } from "shared-store";

let connection = null;
let manualStop = false;
let reconnectAttempt = 0;
const MAX_BACKOFF_MS = 30000;
const queuedInvokes = [];
// const API_URL = import.meta.env.VITE_API_BASE_URL;
const API_URL = "https://localhost:5070";
const environment = import.meta.env.VITE_ENVIRONMENT;

console.log("SignalR api :", API_URL, environment);

export function createConnection(token) {
    if (connection) return connection;
    connection = new signalR.HubConnectionBuilder()
        .withUrl((API_URL) + "/hubs/notifications", {
            accessTokenFactory: () => token,
            withCredentials: false
        })
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Warning)
        .build();

    connection.onreconnecting((err) => {
        console.warn("signal: reconnecting", err);
    });

    connection.onreconnected((id) => {
        console.info("signal: reconnected", id);
        reconnectAttempt = 0;
        flushQueuedInvokes();
    });

    connection.onclose((err) => {
        console.warn("signel: closed", err);
        if (!manualStop) scheduleReconnect();
    });

    return connection;
}

// async function flushQueuedInvokes() {
//     if (!connection || connection.state !== signalR.HubConnectionState.Connected) return;
//     while (queuedInvokes.length) {
//         const { method, args } = queuedInvokes.shift();
//         try {
//             await connection.invoke(method, ...(args || []));
//         } catch (err) {
//             console.error("signal: queued invoke failed", err);
//         }
//     }
// }
// 4. ROBUST QUEUE FLUSHING
async function flushQueuedInvokes() {
    if (!connection || connection.state !== signalR.HubConnectionState.Connected) return;

    console.log(`SignalR: Flushing ${queuedInvokes.length} queued actions`);
    
    while (queuedInvokes.length > 0) {
        const { method, args, resolve, reject } = queuedInvokes.shift();
        try {
            // We await here to ensure order is preserved
            const result = await connection.invoke(method, ...(args || []));
            if (resolve) resolve(result);
        } catch (err) {
            console.error(`SignalR: Failed to invoke buffered method ${method}`, err);
            if (reject) reject(err);
        }
    }
}

function scheduleReconnect() {
    reconnectAttempt++;
    const waitMs = Math.min(MAX_BACKOFF_MS, Math.pow(2, Math.min(6, reconnectAttempt)) * 1000);
    console.info(`SignalR: scheduling reconnect in ${waitMs}ms (attempt ${reconnectAttempt})`);
    setTimeout(async () => {
        // Double check manual stop hasn't happened during the wait
        // if (manualStop) return;
        try {
            await start();
        } catch (e) {
            scheduleReconnect();
        }
    }, waitMs);
}

export async function start() {
    manualStop = false;
    if (!connection) createConnection();
    if (connection.state === signalR.HubConnectionState.Connected) return connection;
    try {
        await connection.start();
        reconnectAttempt = 0;
        console.info("signalR: connected");
        await flushQueuedInvokes();
        return connection;
    } catch (err) {
        console.warn("Signal: start failed", err);
        scheduleReconnect();
        throw err;
    }
}

export async function stop() {
    manualStop = true;
    if (!connection) return;
    console.log("stop trigger");
    try {
        await connection.stop();
        connection = null;
        reconnectAttempt = 0;
        console.info("Signal: stopped");
    } catch (err) {
        console.warn("Signal: stop error", err);
    }
}

export function getConnection() {
    return connection;
}

export function safeInvoke(method, ...args) {
    if (!connection) createConnection();
    if (connection && connection.state === signalR.HubConnectionState.Connected) {
        return connection.invoke(method, ...args);
    } else {
        // queuedInvokes.push({ method, args: Array.isArray(args) ? args : [] });
        // return Promise.resolve();
        // Return a promise that resolves when the connection eventually processes this queue
    return new Promise((resolve, reject) => {
        queuedInvokes.push({ method, args: Array.isArray(args) ? args : [], resolve, reject });
    });
    }
}