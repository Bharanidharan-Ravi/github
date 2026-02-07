import React from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import App from "./App";
// import { AuthProvider } from "./context/AuthContext";
import { useCustomStore } from "shared-store";

window.__MICRO_FRONTEND__ = true;
 useCustomStore.getState().hydrateUserdata();
createRoot(document.getElementById("root")).render(
  // <React.StrictMode>
    <BrowserRouter>
        {/* <AuthProvider> */}
            <App />
        {/* </AuthProvider> */}
    </BrowserRouter>
  // {/* </React.StrictMode> */}
);
