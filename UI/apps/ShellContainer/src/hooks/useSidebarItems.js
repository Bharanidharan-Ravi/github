import { useMemo } from "react";
import { MODULE_DEFINITIONS, useCustomStore } from "shared-store";

export const useSidebarItems = () => {
  const repos = useCustomStore((s) => s.repositories);
  const role = useCustomStore((s) => s.role);
  const basePath = window.location.pathname;
  const splitPath = basePath.split("/").slice(0, 2).join("/"); // Remove leading empty string
  console.log("basePath:", basePath, "splitPath:", splitPath);
  

  return useMemo(() => {
    const items = [];

    // 🔹 Dashboard (no repo)
    items.push({
      key: "dashboard",
      label: "Dashboard",
      icon: "LayoutDashboard",
      path: `${splitPath}/dashboard`,
    });

    // 🔹 Repositories (dynamic)
    items.push({
      key: "repos",
      label: "Repositories",
      icon: "Folder",
      children:
        repos?.map((repo) => ({
          key: repo.Repo_Id,
          label: repo.Title || repo.Repo_Name,
          path: `/${splitPath}/r/${repo.Repo_Id}/tickets`,
        })) || [],
    });

    return items;
  }, [repos, role]);
};
