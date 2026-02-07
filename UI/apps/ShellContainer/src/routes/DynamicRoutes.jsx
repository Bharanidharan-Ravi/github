import { Route, Routes, useParams } from "react-router-dom";
import React, { Suspense, useContext } from "react";
import ProtectedRoute from "./ProtectedRoute";
import { roleRoutes } from "./routesConfig";
import Layout from "../Layout/Layout";

const TicketsApp = React.lazy(() => import("tickets/App"));
const Repository = React.lazy(() => import("repository/App"));

const appMap = {
    repository: <Repository/>,
    tickets: <TicketsApp/>
};

export default function DynamicRoutes({ role }) {
//   const {role} =useParams();
  const routes = roleRoutes[role] || [];
  
    return (
      <Routes>
        <Route element={<Layout role={role} />}>
          {routes.map((route) => (
            <Route
              key={route.path}
              path={"*"}
              element={
               <Suspense >
                    {appMap[route.mfe]}
                </Suspense>
              }
            />
          ))}
        </Route>
      </Routes>
    );
  }