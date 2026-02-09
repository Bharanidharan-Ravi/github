import { create } from "zustand";
import { createGenericSlice } from "./createGenericSlice";

export function createStoreFromModules(modules) {
  return create((set, get) => {
    return modules.reduce((acc, mod) => {
      return {
        ...acc,
        ...createGenericSlice(set, get, mod)
      };
    }, {});
  });
}
