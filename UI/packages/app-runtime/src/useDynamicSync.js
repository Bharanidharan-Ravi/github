import { useEffect } from "react";
import { useLocation } from "react-router-dom";
import { useRuntimeStore } from "./useRuntimeStore";

export function useDynamicSync(modules) {
  const location = useLocation();

  useEffect(() => {
    const segments = location.pathname.split("/").filter(Boolean);

    const repoIndex = segments.indexOf("r");
    const repoId = repoIndex !== -1 ? segments[repoIndex + 1] : null;

    const moduleName =
      repoIndex !== -1 ? segments[repoIndex + 2] : segments[0];

    const module = modules.find((m) => m.name === moduleName);
    if (!module) return;

    const route = module.routes.find((r) => r.path === "");
    if (!route || !route.fetch) return;

    const params = repoId ? { Repo_Id: repoId } : {};

    const store = useRuntimeStore.getState();
    store.fetch(route.fetch.stateKey, params);
  }, [location.pathname]);
}
