export const DashboardDefinition = {
  data: {
    name: "dashboard"
  },
  ui: {
    label: "Dashboard",
    path: "/dashboard",
    icon: "LayoutDashboard"
  },
  routes: [
    {
      path: "",
      key: "view",
      componentKey: "DashboardView"
    }
  ]
};
