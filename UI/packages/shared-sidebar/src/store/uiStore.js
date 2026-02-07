// src/store/uiStore.js
import { create } from "zustand";

export const useUIStore = create((set) => ({
  sidebarItems: [],
  isSidebarOpen: false,

  setSidebarItems: (items) => set({ sidebarItems: items }),
  openSidebar: () => set({ isSidebarOpen: true }),
  closeSidebar: () => set({ isSidebarOpen: false }),
}));
