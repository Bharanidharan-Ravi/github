// src/router/CentralRouter.jsx
import React, { Suspense, useContext } from "react";
import { Routes, Route, Navigate, useNavigate } from "react-router-dom";
import ProtectedRoute from "./ProtectedRoute";
import DynamicRoutes from "./DynamicRoutes";
import { roleRoutes } from "./routesConfig";
import { decryptUserInfo } from "../SharedFile/Decryption/Decryption";
import { useCustomStore } from "shared-store";
import { useState } from "react";
import { createConnection, start, joinGroup } from "shared-signalr";

// Lazy load all remotes
const AuthApp = React.lazy(() => import("auth/App"));

export default function CentralRouter() {
  const { user, token } = useCustomStore();
  const navigate = useNavigate();
  const userdata = sessionStorage.getItem("user");
  const [jwtToken, setToken] = useState(user?.JwtToken || null);
  const [role, setRole] = useState(user?.Role || null);

  const routes = roleRoutes[role] || [];

  const handleLogin = (result) => {
    // const userData = decryptUserInfo(result)[0];
    const role = result?.Role;
    const token = result?.JwtToken;
    setRole(role);
    setToken(token);
    // start();
    sessionStorage.setItem("role", role);
    const routes = roleRoutes[role] || [];
    if (routes.length > 0) {
      const firstRoute = routes[0]?.path; 
      navigate(firstRoute, {replace: true}); 
    } else {
      navigate("/unauthorized", {replace: true}); // If no routes are available, redirect to unauthorized
    }
  }
  return (
    <Suspense fallback={<div>Loading...</div>}>
      <Routes>
        {/* Public Routes */}
        <Route path="/auth/*" element={<AuthApp onLogin={handleLogin} />} />

        <Route path="/:role/*" element={
          <ProtectedRoute 
            allowedRoutes={routes} 
            userData={userdata}>
            <DynamicRoutes role={role} />
          </ProtectedRoute>} />
        <Route path="*" element={<Navigate to="/auth" replace />} />
      </Routes>
    </Suspense>
  );
}

// export default function CentralRouter() {
//   const { handleLogin, handleLogout } = useContext(routingContext);
//   return (
//     <Suspense fallback={<div>Loading...</div>}>
//       <Routes>
//         {/* Public Routes */}
//         <Route element={<Layout />}>
//           <Route path="/auth/*" element={<AuthApp onLogin={handleLogin} />} />
//           <Route path="/employee" element={<Navigate to="/employee/dashboard" replace />} />
//           <Route path="/client" element={<Navigate to="/client/dashboard" replace />} />

//           {/* Role-Protected Routes */}
//           <Route
//             path="/client/*"
//             element={
//               <ProtectedRoute allowedRoles={["3"]}>
//                 <ClientApp />
//               </ProtectedRoute>
//             }
//           />
//           <Route
//             path="/employee/*"
//             element={
//               <ProtectedRoute allowedRoles={["2"]}>
//                 <EmployeeApp />
//               </ProtectedRoute>
//             }
//           />
//           <Route
//           path="/Repository/*"
//           element ={
//             <Repository/>
//           }/>
//           {/* <Route
//           path="/admin/*"
//           element={
//             <ProtectedRoute allowedRoles={["1"]}>
//               <AdminApp />
//             </ProtectedRoute>
//           }
//         /> */}

//           {/* Fallbacks */}
//           <Route path="/unauthorized" element={<div>Unauthorized</div>} />
//           <Route path="*" element={<Navigate to="/auth" replace />} />
//         </Route>
//       </Routes>
//     </Suspense>
//   );
// }
