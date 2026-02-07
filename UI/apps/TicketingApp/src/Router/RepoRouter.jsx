import React, { Suspense, useEffect } from 'react';
import { Routes, Route } from 'react-router-dom';
import RepoLayout from '../components/Repository/Layout/RepoLayout';
import ViewRepo from '../components/Repository/Pages/ViewRepo';
import RepoDetails from '../components/Repository/Pages/RepoDetails';
import CreateRepo from '../components/Repository/Pages/CreateRepo';

export default function RepoRouter({role}) {
  // const {showCreate} = useTicketsContext();
  // const location = useLocation();
  // const backgroundLocation = location.state && location.state.backgroundLocation;
 
  return (
    <Suspense fallback={<div>Loading...</div>}>
      <Routes>
        <Route element={<RepoLayout />}>
          <Route path="/" element={<ViewRepo role={role} />} />
          <Route path=":repoIds" element={<RepoDetails />} />
          <Route path="create" element={<CreateRepo />} />
        </Route>
      </Routes>
       {/* {backgroundLocation && (
        <Routes>
          <Route path="create" element={<CreateRepo isModal />} />
        </Routes>
      )} */}
    </Suspense>
  );
}
