export const ProjectDefinition = {
  data: {
    name: "projects"
  },

  ui: {
    label: "Projects",
    icon: "Folder",
    path: "projects"
  },

  routes: [
    {
      path: "",
      key: "list",
      componentKey: "ProjectList",
      fetcherName: "EnsureProjects"
    },
    {
      path: "create",
      key: "create",
      componentKey: "ProjectCreate"
    }
  ]
};
