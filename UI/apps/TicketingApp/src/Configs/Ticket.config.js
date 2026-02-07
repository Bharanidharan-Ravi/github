export const TicketModule = {
  name: "tickets",

  sidebar: {
    label: "Tickets",
    path: "tickets",
  },

  routes: [
    {
      path: "",                       // /tickets OR /r/:repoId/tickets
      componentKey: "TicketList",

      fetch: {
        url: "/tickets",
        method: "POST",

        stateKey: "TicketList",
        readyKey: "TicketList_ready",
        idKey: "Ticket_id",
      },
    },
  ],
};
