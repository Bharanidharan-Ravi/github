import React from "react";

/* ---------------- DASHBOARD ---------------- */
const DashboardView = React.lazy(() =>
  import("../components/Dashboard/Pages/DashboardView")
);

/* ---------------- TICKETS ---------------- */
const ViewTickets = React.lazy(() =>
  import("../components/Tickets/Pages/ViewTickets")
);
const CreateTicket = React.lazy(() =>
  import("../components/Tickets/Pages/CreateTicket")
);
const TicketDetails = React.lazy(() =>
  import("../components/Tickets/Pages/TicketDetails")
);

/* ---------------- PROJECTS ---------------- */
const ProjectList = React.lazy(() =>
  import("../components/Projects/Pages/ViewProjects")
);
const ProjectCreate = React.lazy(() =>
  import("../components/Projects/Pages/CreateProject")
);

/* ---------------- FALLBACK ---------------- */
const NotFound = () => (
  <div style={{ padding: 20 }}>Component Not Found</div>
);

export const ComponentRegistry = {
  components: {
    DashboardView,

    ViewTickets,
    CreateTicket,
    TicketDetails,

    ProjectList,
    ProjectCreate,

    NotFound,
  },
};
