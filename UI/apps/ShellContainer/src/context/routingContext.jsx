// src/context/routingContext.jsx
import React, { createContext, useState } from "react";
// import { useDispatch } from "react-redux";
import { useNavigate } from "react-router-dom";
import { loginUser } from "../apiService/LoginService/loginThunk";
import { decryptUserInfo } from "../SharedFile/Decryption/Decryption";
import { roleRoutes } from "../routes/routesConfig";
import { useCustomStore, loginThunk } from "shared-store";

export const routingContext = createContext();

export const RoutingProvider = ({ children }) => {
  const { user } = useCustomStore();
  const navigate = useNavigate();
  // const dispatch = useDispatch();
  const [isAuthenticated, setIsAuthenticated] = useState(
    !!localStorage.getItem("token")
  );
  const handleLogout = () => {
    localStorage.removeItem("token", "dummy-token");
    setIsAuthenticated(true);
    navigate("/auth");
  }
  return (
    <routingContext.Provider value={{ handleLogin, isAuthenticated, handleLogout }}>
      {children}
    </routingContext.Provider>
  );
};
