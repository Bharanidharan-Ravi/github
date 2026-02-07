import { MODULE_DEFINITIONS } from "../config/MODULE_DEFINITIONS";
import { GetAllReponew } from "../Thunks/RepoThunk";
import { createGenericSlice } from "./createGenericSlice";

export const createRepoSlice = (set, get) => ({
   ...createGenericSlice(set, get, MODULE_DEFINITIONS.repository),   
});