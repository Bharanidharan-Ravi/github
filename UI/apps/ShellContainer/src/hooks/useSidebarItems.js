// shell-app/hooks/useSidebarItems.js
import { useMemo } from "react";
import { MODULE_DEFINITIONS, useCustomStore } from "shared-store";

export const useSidebarItems = () => {
  const repos = useCustomStore(state => state.repositories);

  return useMemo(() => {
    return Object.values(MODULE_DEFINITIONS).map(module => {
console.log("MODULE_DEFINITIONS:", MODULE_DEFINITIONS, module);

      // 🔹 Repository Section
      if (module.data.name === "repo") {
        return {
          key: "repositories",
          label: module.ui.label,
          icon: module.ui.icon,
          children: repos?.map(repo => ({
            key: repo.Repo_Id,
            label: repo.Title || repo.Repo_Name,
            path: `/r/${repo.Repo_Id}/tickets`
          })) || []
        };
      }

      // 🔹 Normal Modules
      return {
        key: module.data.name,
        label: module.ui.label,
        icon: module.ui.icon,
        path: module.ui.path
      };
    });
  }, [repos]);
};
