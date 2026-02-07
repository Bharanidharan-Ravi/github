

export const TicketDefinition = {
  data: {
    name: "tickets",
    listKey: "TicketMaster",
    idKey: "Ticket_id",
    eventPrefix: "EmployeeTicket"
  },

  ui: {
    label: "Tickets",
    icon: "Ticket",
    path: "tickets",   // 👈 RELATIVE TO /r/:repoId
  },

  routes: [
    {
      path: "",
      key: "list",
      componentKey: "ViewTickets",
      fetcherName: "EnsureTicketMaster",
      api: DynamicGetData,
      configKeys: [
        {
          key: "TicketList",
          sourceKey: "TicketMaster",
          param: true,          // Repo_Id auto injected
          merge: true
        }
      ]
    },
    {
      path: "create",
      key: "create",
      componentKey: "CreateTicket",
      fetcherName: "EnsureCreateTicket"
    },
    {
      path: "view/:Ticket_id",
      key: "detail",
      componentKey: "TicketDetails",
      fetcherName: "EnsureTicketDetails"
    }
  ]
};
