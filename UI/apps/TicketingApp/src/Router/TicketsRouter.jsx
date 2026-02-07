import React, { Suspense, useEffect } from 'react';
import { Routes, Route, Navigate, useNavigate, useLocation } from 'react-router-dom';
import ViewTickets from '../components/Tickets/Pages/ViewTickets';
import CreateTicket from '../components/Tickets/Pages/CreateTicket';
import TicketDetails from '../components/Tickets/Pages/TicketDetails';
import TicketLayout from '../components/Tickets/Layout/TicketLayout';
import { useTicketStore } from '../components/Tickets/TicketStore/TicketStore';
// import CreateTicket from '../components/Tickets/Pages/CreateTicket';

export default function TicketsRoutes({role}) {
  // const {showCreate} = useTicketsContext();
  const { showForm } = useTicketStore();
  const location = useLocation();
  const backgroundLocation = location.state && location.state.backgroundLocation;
 
  return (
    <Suspense fallback={<div>Loading...</div>}>
      <Routes location={backgroundLocation || location}>
        <Route element={<TicketLayout />}>
          <Route path="/" element={<ViewTickets role={role} />} />
          <Route path=":ticketId" element={<TicketDetails />} />
          <Route path="create" element={<CreateTicket />} />
        </Route>
      </Routes>
    </Suspense>
  );
}
