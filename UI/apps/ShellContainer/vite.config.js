import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import federation from "@originjs/vite-plugin-federation";
import path from "path";
import { createRequire } from "module";

const require = createRequire(import.meta.url);

// Helper function to resolve package.json paths
function resolvePackageJson(packageName) {
  try {
    const entryPath = require.resolve(packageName);
    return path.resolve(path.dirname(entryPath), "../package.json");
  } catch (e) {
    return null;
  }
}

const deps = require("./package.json").dependencies;

export default defineConfig({
  plugins: [
    react(),
    federation({
      name: "shell",
      remotes: {
        auth: "http://localhost:5002/assets/remoteEntry.js",
        repository: "http://localhost:6003/assets/remoteEntry.js",
        tickets: "http://localhost:6003/assets/remoteEntry.js",
      },
      shared: {
        // --- React & Core ---
        react: { singleton: true, requiredVersion: deps.react },
        "react-dom": { singleton: true, requiredVersion: deps["react-dom"] },
        "react-router-dom": { singleton: true, requiredVersion: deps["react-router-dom"] },

        // --- State ---
        zustand: { singleton: true, requiredVersion: deps.zustand },
        "@reduxjs/toolkit": { singleton: true, requiredVersion: deps["@reduxjs/toolkit"] },
        "react-redux": { singleton: true, requiredVersion: deps["react-redux"] },

        // --- Shared Local Packages ---
        "shared-store": { singleton: true },
        "shared-signalr": { singleton: true },

        // --- Tiptap (Hardcoded Stable v2) ---
        "@tiptap/core": { singleton: true, requiredVersion: "^2.10.4" },
        "@tiptap/react": { singleton: true, requiredVersion: "^2.10.4" },
        "@tiptap/starter-kit": { singleton: true, requiredVersion: "^2.10.4" },
        "@tiptap/extension-image": { singleton: true, requiredVersion: "^2.10.4" },
        "@tiptap/extension-link": { singleton: true, requiredVersion: "^2.10.4" },
        "@tiptap/extension-mention": { singleton: true, requiredVersion: "^2.10.4" },
        "@tiptap/extension-placeholder": { singleton: true, requiredVersion: "^2.10.4" },
        "@tiptap/extension-table": { singleton: true, requiredVersion: "^2.10.4" },
        "@tiptap/extension-table-cell": { singleton: true, requiredVersion: "^2.10.4" },
        "@tiptap/extension-table-header": { singleton: true, requiredVersion: "^2.10.4" },
        "@tiptap/extension-table-row": { singleton: true, requiredVersion: "^2.10.4" },
        "@tiptap/extension-underline": { singleton: true, requiredVersion: "^2.10.4" },

        // --- ProseMirror ---
        "prosemirror-state": { singleton: true, requiredVersion: "^1.4.3" },
        "prosemirror-view": { singleton: true, requiredVersion: "^1.34.3" },
        "prosemirror-model": { singleton: true, requiredVersion: "^1.19.4" },
        "prosemirror-transform": { singleton: true, requiredVersion: "^1.8.0" },
      },
    }),
  ],
  resolve: {
    alias: {
      // 1. Existing local aliases
      "shared-store": path.resolve(__dirname, "../../packages/shared-store"),
      "shared-signalr": path.resolve(__dirname, "../../packages/shared-signalr"),
         "shared-table": path.resolve(
              __dirname,
              "../../packages/DynamicTable/src"
            ),
            "shared-form": path.resolve(
              __dirname,
              "../../packages/react-input-engine/src"
            ),
 "shared-Sidebar": path.resolve(
              __dirname,
              "../../packages/shared-sidebar/src"
            ),
      // 2. Tiptap Aliases (Existing)
      "@tiptap/core/package.json": resolvePackageJson("@tiptap/core"),
      "@tiptap/react/package.json": resolvePackageJson("@tiptap/react"),
      "@tiptap/starter-kit/package.json": resolvePackageJson("@tiptap/starter-kit"),
      "@tiptap/extension-image/package.json": resolvePackageJson("@tiptap/extension-image"),
      "@tiptap/extension-link/package.json": resolvePackageJson("@tiptap/extension-link"),
      "@tiptap/extension-mention/package.json": resolvePackageJson("@tiptap/extension-mention"),
      "@tiptap/extension-placeholder/package.json": resolvePackageJson("@tiptap/extension-placeholder"),
      "@tiptap/extension-table/package.json": resolvePackageJson("@tiptap/extension-table"),
      "@tiptap/extension-table-cell/package.json": resolvePackageJson("@tiptap/extension-table-cell"),
      "@tiptap/extension-table-header/package.json": resolvePackageJson("@tiptap/extension-table-header"),
      "@tiptap/extension-table-row/package.json": resolvePackageJson("@tiptap/extension-table-row"),
      "@tiptap/extension-underline/package.json": resolvePackageJson("@tiptap/extension-underline"),

      // 3. FIX: Add ProseMirror Aliases Here
      "prosemirror-state/package.json": resolvePackageJson("prosemirror-state"),
      "prosemirror-view/package.json": resolvePackageJson("prosemirror-view"),
      "prosemirror-model/package.json": resolvePackageJson("prosemirror-model"),
      "prosemirror-transform/package.json": resolvePackageJson("prosemirror-transform"),
    },
  },
  server: {
    port: 5173,
  },
  build: {
    target: "esnext",
  },
});