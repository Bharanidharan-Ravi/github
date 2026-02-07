import { defineConfig } from 'vite';
import path from "path";

export default defineConfig({
  build: {
    lib: {
      entry: path.resolve(__dirname, "./src/index.js"),
      name: "shared-signalr",
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
      "shared-store": path.resolve(__dirname, "../../packages/shared-store/src"),
    },
  },
});

