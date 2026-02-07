export const TicketModule = {
  data: {
    name: "tickets",
    dataKey: "TicketMaster",
    readyKey: "isTicketReady",
    idKey: "Ticket_id",
    api: async (payload) => {
      // TEMP API FOR TESTING
      console.log("API PAYLOAD:", payload);
      return [
        { Ticket_id: 1, title: "Bug" },
        { Ticket_id: 2, title: "Feature" }
      ];
    }
  },
  ui: {
    routes: []
  }
};
