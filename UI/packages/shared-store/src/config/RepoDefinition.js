// repository/repo.config.js

export const RepoDefinition = {
  // ======================
  // DATA IDENTITY
  // ======================
  data: {
    name: "repo",          // used by shell to identify repository module
    entity: "Repository",  // optional: DB / domain name
  },

  // ======================
  // UI DECLARATION
  // ======================
  ui: {
    label: "Repositories",
    icon: "Folder",        // lucide-react icon name

    /**
     * IMPORTANT:
     * Repo module does NOT define a static path like /repository
     * Because repo instances are dynamic (/r/:RepoId)
     */
    path: null,

    /**
     * This tells the SHELL:
     * "I need dynamic children injected from API"
     */
    // dynamicChildren: {
    //   source: "repositories",   // shell store key
    //   keyField: "Repo_Id",
    //   labelField: "Repo_Name",   // or Title
    //   icon: "Circle",

    //   /**
    //    * Base navigation target when a repo is clicked
    //    * Shell will append Repo_Id automatically
    //    */
    //   navigateTo: "/r/:Repo_Id/tickets",
    // },
  },

  // ======================
  // ROUTES (repo-scoped)
  // ======================
  routes: [
    {
      key: "repoTickets",
      path: "/r/:Repo_Id/tickets",
      componentKey: "TicketList",
      fetcherName: "ensureTicketsLoaded",
    },
    {
      key: "repoProjects",
      path: "/r/:Repo_Id/projects",
      componentKey: "ProjectList",
      fetcherName: "ensureProjectsLoaded",
    },
  ],
};
