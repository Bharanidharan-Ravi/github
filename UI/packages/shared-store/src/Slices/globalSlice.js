export const createGlobalSlice = (set, get) => ({
  globalLoading: 0,
  globalError: null,
  globalSuccess: null,

  setGlobalLoading: (isLoading) =>
    set((state) => {
      const currentCount = state.globalLoading || 0;

      if (isLoading) {
        // New request started -> Increment
        return { globalLoading: currentCount + 1 };
      } else {
        // Request finished -> Decrement (but never go below 0)
        return { globalLoading: Math.max(0, currentCount - 1) };
      }
    }),
  setGlobalError: (msg) => set({ globalError: msg }),
  clearGlobalError: () => set({ globalError: null }),
  setGlobalSuccess: (msg) => set({ globalSuccess: msg }),
  clearGlobalSuccess: () => set({ globalSuccess: null }),
});
