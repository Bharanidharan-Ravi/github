import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import federation from "@originjs/vite-plugin-federation";
import path from "path";
const deps = require("./package.json").dependencies;
export default defineConfig({
  plugins: [
    react(),
    federation({
      name: "tickets",
      filename: "remoteEntry.js",
      remotes: {
        shell: "http://localhost:5173/assets/remoteEntry.js", // Point to the parent app
      },
      exposes: {
        "./App": "./src/App.jsx", // Exposing the main App component
        // './RepositoryRouter': './src/Router/RepositoryRouter.jsx',
        // other components...
      },
      dev: true,
      //   shared: {
      //     "react": { singleton: true },
      //     "react-dom": { singleton: true },
      //     "react-router-dom": { singleton: true },
      //     "zustand": { singleton: true },
      //     "shared-store": { singleton: true },
      //     "@tiptap/react": { singleton: true },
      //     "shared-signalr": { singleton: true }
      //   }
      // }),
      shared: {
        // --- React Core ---
        react: { requiredVersion: deps.react, singleton: true },
        "react-dom": { requiredVersion: deps["react-dom"], singleton: true },
        "react-router-dom": {
          requiredVersion: deps["react-router-dom"],
          singleton: true,
        },

        // --- State Management ---
        zustand: { requiredVersion: deps.zustand, singleton: true },
        "@reduxjs/toolkit": {
          requiredVersion: deps["@reduxjs/toolkit"],
          singleton: true,
        },
        "react-redux": {
          requiredVersion: deps["react-redux"],
          singleton: true,
        },

        // --- Shared Business Logic ---
        "shared-store": { singleton: true },
        "shared-signalr": { singleton: true },

        // --- Tiptap & ProseMirror (Must match Parent) ---
        "@tiptap/core": { singleton: true },
        "@tiptap/react": { singleton: true },
        "@tiptap/starter-kit": { singleton: true },
        "@tiptap/extension-image": { singleton: true },
        "@tiptap/extension-link": { singleton: true },
        "@tiptap/extension-mention": { singleton: true },
        "@tiptap/extension-placeholder": { singleton: true },
        "@tiptap/extension-table": { singleton: true },
        "@tiptap/extension-table-cell": { singleton: true },
        "@tiptap/extension-table-header": { singleton: true },
        "@tiptap/extension-table-row": { singleton: true },
        "@tiptap/extension-underline": { singleton: true },

        // Explicitly share ProseMirror
        "prosemirror-state": { singleton: true },
        "prosemirror-view": { singleton: true },
        "prosemirror-model": { singleton: true },
        "prosemirror-transform": { singleton: true },
      },
    }),
  ],
  resolve: {
    alias: {
      "shared-store": path.resolve(
        __dirname,
        "../../packages/shared-store/src"
      ),
      "shared-signalr": path.resolve(
        __dirname,
        "../../packages/shared-signalr/src"
      ),
      "shared-table": path.resolve(
        __dirname,
        "../../packages/DynamicTable/src"
      ),
      "shared-form": path.resolve(
        __dirname,
        "../../packages/react-input-engine/src"
      )
    },
  },
  server: {
    port: 5003,
  },
  preview: {
    port: 6003,
  },
  build: {
    target: "esnext",
    modulePreload: false,
    cssCodeSplit: false,
    sourcemap: false,
    minify: "terser",
  },
});
