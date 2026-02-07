import { useEffect, useMemo, useRef } from "react";
import { useLocation } from "react-router-dom";
import { MODULE_DEFINITIONS, useCustomStore } from "shared-store";
import { matchPattern } from "../Shared/Utilities/Utilities";

export function useDynamicSync() {
  const location = useLocation();
  const pathname = location.pathname;
  const store = useCustomStore.getState();
  const lastFetchRef = useRef("");
  const user = useCustomStore.getState();
  
  const segments = useMemo(
    () => pathname.split("/").filter(Boolean),
    [pathname],
  );

  /* ---------------- GLOBAL PARAMS ---------------- */
  const globalParams = useMemo(() => {
    const repoIndex = segments.indexOf("r");
    if (repoIndex !== -1 && segments[repoIndex + 1]) {
      return { Repo_Id: segments[repoIndex + 1] };
    }
    return {};
  }, [segments]);

  /* ---------------- MODULE + ROUTE ---------------- */
  const resolved = useMemo(() => {
    const repoIndex = segments.indexOf("r");
    const isRepoScoped = repoIndex !== -1 && segments[repoIndex + 1];

    let moduleSegment;

    if (isRepoScoped) {
      // /user/r/:repoId/:module
      moduleSegment = segments[repoIndex + 2];
    } else {
      // /user/dashboard OR /dashboard
      moduleSegment =
        segments[0] === user.UserName || segments[0] === "admin"
          ? segments[1]
          : segments[0];
    }

    const moduleKey = Object.keys(MODULE_DEFINITIONS).find(
      (key) => key === moduleSegment,
    );

    console.log("resolver:", {
      segments,
      isRepoScoped,
      moduleSegment,
      moduleKey,
      MODULE_DEFINITIONS
    });

    if (!moduleKey) return {};

    const moduleDef = MODULE_DEFINITIONS[moduleKey];

    let relativeSegments;

    if (isRepoScoped) {
      relativeSegments = segments.slice(repoIndex + 3);
    } else {
      const moduleIndex = segments.indexOf(moduleSegment);
      relativeSegments = segments.slice(moduleIndex + 1);
    }

    let activeRoute;
    let routeParams = {};

    for (const route of moduleDef.ui.routes) {
      // root route
      if (route.path === "" && relativeSegments.length === 0) {
        activeRoute = route;
        break;
      }

      const params = matchPattern(route.path, relativeSegments);
      if (params) {
        activeRoute = route;
        routeParams = params;
        break;
      }
    }

    return { moduleDef, activeRoute, routeParams };
  }, [segments]);

  /* ---------------- FETCH ---------------- */
  useEffect(() => {
    if (!resolved.activeRoute?.fetcherName) return;

    const params = {
      ...globalParams,
      ...resolved.routeParams,
    };

    const paramsKey = JSON.stringify(params);
    const fetchKey = `${resolved.activeRoute.fetcherName}:${paramsKey}`;

    if (lastFetchRef.current === fetchKey) return;
    lastFetchRef.current = fetchKey;

    const fetcher = store[resolved.activeRoute.fetcherName];
    if (typeof fetcher === "function") {
      fetcher(params);
    }
  }, [resolved, globalParams, store]);

  return resolved;
}
