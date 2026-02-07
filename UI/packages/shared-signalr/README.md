# shared-signalr

shared signalr helpers for micro-frontend apps.

## Build 
pnpm --filter shared-signalr run build 

## Exports
- createConnection(), start(), stop(), getConnection(), safeInvoke()
- addEventHandler(), removeEventHandler(), clearAllHandlers()
- joinGroup(name), leaveGroup(name)
- Hooks: useSignalR(), useGroup(), useEvent()

## typical usage

### shell (on login)
```js
import { createConnection, start } from "shared-signalr/connection";
import { reattachAllHandlers } from "shared-Signalr/events";
import { useCustomStore } from "shared-store";

createConnection();
await start();
reattachAllHandlers();