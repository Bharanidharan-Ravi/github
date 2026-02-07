export const createTicketSlice = (set, get) => ({
    Repo: null,
    TicketMaster: null,
    updateRepo: [],
    masterProject: [],
    updateProject: [],
    TicketsByID: [],
    ReturnTemp: [],
    ThreadList:[],

    setRepoData: (Repo) => set({ Repo }),
    setTicketMaster: (TicketMaster) => set({ TicketMaster }),
    addUpdateRepo: (item) =>
        set((state) => ({
            Repo: [...state.Repo, item],
        })),
    setProject: (proj) =>
        set({ masterProject: proj }),
    addUpdateProj: (item) =>
        set((state) => ({
            masterProject: [...state.masterProject, item],
        })),
    setProjTickets: (tickets) =>
        set({ TicketsByID: tickets }),
    setReturnTemp: (temp) =>
        set({ ReturnTemp: temp }),
    setThreadList: (data) => 
        set({ ThreadList: data },),
    ensureRepoLoaded: async () => {
        const state = get();
        
        // CASE 1: First Load (Show Loading Spinner)
        if (!state.isTicketsInitialized) {
            console.log("⬇️ Fetching Tickets (First Load)...");
            // You might want a 'loading' flag here if you use spinners
            const res = await GetAllRepo();
            const data = await res.json();
            set({ tickets: data, isTicketsInitialized: true });
            return;
        }

        // CASE 2: Re-visiting the page (Background Refresh)
        // We already have data, so we don't block the UI.
        // We just fetch silently to catch up on missed SignalR events.
        console.log("🔄 Syncing Tickets (Background)...");
        try {
            const res = await fetch("/api/tickets/getall");
            const data = await res.json();
            
            // Only update if data is actually different (Optional optimization)
            set({ tickets: data }); 
        } catch (err) {
            console.warn("Background sync failed", err);
        }
    }
});