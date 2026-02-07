// src/router/ProtectedRoute.jsx
import React from "react";
import { matchPath, Navigate, useLocation, useNavigate } from "react-router-dom";
import { jwtDecode } from "jwt-decode";
import { decryptUserInfo } from "../SharedFile/Decryption/Decryption";
import { createConnection, stop } from "shared-signalr";
import { getDecryptedUser, isTokenValid } from "../SharedFile/Helper/Utlities";

export default function ProtectedRoute({ children, allowedRoutes,userData }) {
  const location = useLocation();  
 if (!userData) {  
    sessionStorage.clear();
    stop();
    return <Navigate to="/auth" replace />;
  }
 
  const currentPath = location.pathname;

  const isRouteAllowed = allowedRoutes.some(route =>
    matchPath(route.path, currentPath)
  );

  if (!isRouteAllowed) {
    sessionStorage.clear();
    stop();
    return <Navigate to="/unauthorized" replace />;
  }

  return children;
}


