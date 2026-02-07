import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import federation from '@originjs/vite-plugin-federation';
import path from "path";

export default defineConfig({
  plugins: [
    react(),
    federation({
      name: 'auth',
      filename: 'remoteEntry.js',
      exposes: {
        './App': './src/App.jsx', // ✅ must point to a file with default export
      },
      dev: true,
      shared: {
        "react":{ singleton: true, eager: true},
        "react-dom": { singleton: true, eager: true},
        "react-router-dom": { singleton: true, eager: true},
        "shared-store": {singleton: true, eager: true}
      }
    }),
  ],
  resolve: {
    alias: {
      "shared-store": path.resolve(__dirname, "../../packages/shared-store/src"),
    },
  },
  server: {
    port: 5002,
  },
  preview: {
    port: 5002,
  },
  build: {
    target: 'esnext',
    modulePreload: false,
    cssCodeSplit: false,
    minify: false,
  },
})

