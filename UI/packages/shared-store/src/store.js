import { create } from "zustand";
import { createAuthSlice } from "./Slices/LoginSlice";
import { createGlobalSlice } from "./Slices/globalSlice";
import { createTicketSlice } from "./Slices/TicketSlice";
import { CreateActiveSlice } from "./Slices/GlobalActiveSlice";
import { createMasterDataSlice } from "./Slices/MasterDataSlice";
import { createRepoSlice } from "./Slices/RepoSlice";

export const useCustomStore = create((set, get) => ({
    ...createAuthSlice(set, get),
    ...createGlobalSlice(set, get),
    ...createTicketSlice(set, get),
    ...CreateActiveSlice(set, get),
    ...createMasterDataSlice(set, get),
    ...createRepoSlice(set, get),
}))
