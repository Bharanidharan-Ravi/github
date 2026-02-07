import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import federation from '@originjs/vite-plugin-federation';
import path from "path";

export default defineConfig({
  plugins: [react()],
  build: {
    lib: {
      entry: path.resolve(__dirname, "./src/index.js"),
      name: "shared-store",
      fileName: "index.js",
      formats: ["es"]
    },
    rollupOptions: {
      external: ["react", "react-dom", "zustand", "axios"],
      output: {
        globals: {
          react: "React",
          "react-dom": "ReactDom",
          zustand: "zustand",
          axios: "axios"
        }
      }
    },
    outDir: "dist",
    emptyOutDir: true
  },
});

