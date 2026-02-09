import { defineConfig } from 'vite';
import path from "path";

export default defineConfig({
  build: {
    lib: {
      entry: path.resolve(__dirname, "./src/index.js"),
      name: "app-runtime",
      fileName: "index.js",
      formats: ["es"]
    },

    outDir: "dist",
    emptyOutDir: true,

    rollupOptions: {
      external: ["react", "react-dom", "zustand", "@microsoft/signalr",
         "shared-store"]
    },
  },

  resolve: {
    alias: {
      "zustand-data-orchestrator": path.resolve(__dirname, "../../packages/zustand-data/src"),
    },
  },
});

