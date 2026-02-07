import React, { useEffect } from "react";
import {
  Routes,
  Route,
  BrowserRouter,
  Navigate,
  useNavigate,
} from "react-router-dom";
// import { TicketsProvider } from './components/Tickets/Context/TicketsContext.jsx';
import TicketsRoutes from "./Router/TicketsRouter.jsx";
import Navbar from "./Shared/Navbar/navbar.jsx";
import Dashboard from "./components/Dashboard/Pages/ViewTickets.jsx";
import { DashboardProvider } from "./components/Dashboard/Context/DashboardContext.jsx";
import RepoRouter from "./Router/RepoRouter.jsx";
import { useCustomStore } from "shared-store";
import ProjectRouter from "./Router/ProjectRouter.jsx";
import { AppDataSync } from "./Shared/AppDataSync/AppDataSync.jsx";
import CRMmainPage from "./components/CRM/crmMainPage.jsx";

function App() {
  const isStandalone = !window.__MICRO_FRONTEND__;
  // const { role } = useCustomStore();
  const role = useCustomStore((state) => state.role);
  const setActiveModule = useCustomStore((state) => state.setActiveModule);

  // useEffect(() => {
  //   setActiveModule("tickets");
  // }, []);

  // When embedded in shell, basePath = admin | employee | client
  const basePath = window.location.pathname.split("/")[1];
  const RouterWrapper = ({ children }) =>
    isStandalone ? <BrowserRouter>{children}</BrowserRouter> : <>{children}</>;

  return (
    <RouterWrapper>

      {/* 🔹 Navbar is aware of basePath (role prefix) */}
      {/* <Navbar basePath={basePath} role={2} /> */}
      <AppDataSync />
      <Routes>
        <Route path="/" element={<Navigate to="/dashboard" replace />} />
        <Route path="/CRM" element={<CRMmainPage />} />
        <Route
          path={`/dashboard/*`}
          element={
            <DashboardProvider>
              <Dashboard role={role} />
            </DashboardProvider>
          }
        />
        <Route path={`/tickets/*`} element={<TicketsRoutes role={role} />} />
        <Route path={`/repository/*`} element={<RepoRouter role={role} />} />
        <Route path={`/projects/*`} element={<ProjectRouter role={role} />} />
      </Routes>
    </RouterWrapper>
  );
}
export default App;
