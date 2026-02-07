import { create } from "zustand";

export const useTdsFormStore = create((set) => ({
  values: {},
  setValue: (k, v) =>
    set(s => ({ values: { ...s.values, [k]: v } })),
  reset: () => set({ values: {} })
}));
