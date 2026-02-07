import React, { useState, useEffect } from 'react';
import { Outlet } from 'react-router-dom';
import ViewTickets from '../Pages/ViewTickets';

const TicketLayout = () => {
  return (
    // <div className="layout-container">
      <Outlet />
    // </div>
  );
};

export default TicketLayout;
