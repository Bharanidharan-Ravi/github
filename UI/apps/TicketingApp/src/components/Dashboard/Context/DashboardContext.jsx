import React, { createContext, useState, useContext, useEffect } from 'react'

// Create Dashboard context
const DashboardContext = createContext()

// Dashboard context provider
export function DashboardProvider({ children }) {
  const [dashboardData, setDashboardData] = useState(null);

  useEffect(() => {
    setDashboardData("dasboardcontext activated")
  },[])

  const fetchDashboardData = async () => {
    // Simulate fetching dashboard data
    const data = { userCount: 100, ticketCount: 200 }
    setDashboardData(data)
  }
  const [tickets, setTickets] = useState([])

  // const fetchTickets = async () => {
  //   // Simulate fetching tickets data
  //   const data = [{ id: 1, title: 'Issue 1' }, { id: 2, title: 'Issue 2' }]
  //   setTickets(data)
  // }

  const fetchTickets = async () => {
    const data = [
      {
        id: 1,
        title: 'Bug in login flow',
        description: 'Login button not working on mobile devices',
        status: 'open',
        labels: ['bug', 'urgent'],
        assignee: 'John Doe',
        createdAt: '2025-11-01',
        comments: 4,
      },
      {
        id: 2,
        title: 'Add dark mode',
        description: 'Implement dark mode for the dashboard',
        status: 'closed',
        labels: ['enhancement'],
        assignee: 'Jane Smith',
        createdAt: '2025-10-28',
        comments: 2,
      },
    ];
    setTickets(data);
  };


  return (
    <DashboardContext.Provider value={{ dashboardData, fetchDashboardData,tickets, fetchTickets }}>
      {children}
    </DashboardContext.Provider>
  )
}

// Custom hook to access the Dashboard context
export const useDashboardContext = () => {
  return useContext(DashboardContext)
}
