import { createStoreFromModules } from "zustand-data-orchestrator";
import { TicketModule } from "../modules/tickets";

export const useAppStore = createStoreFromModules([
  TicketModule
]);