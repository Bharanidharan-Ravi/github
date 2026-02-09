import React, { Suspense, useMemo } from "react";
import { useLocation } from "react-router-dom";
import { MODULE_DEFINITIONS } from "shared-store";
import { ComponentRegistry } from "../registry/ComponentRegistry";
import { matchPattern } from "../Shared/Utilities/Utilities";

/**
 * Resolves:
 * - active module
 * - active route
 * - renders component
 */
const SmartRouteWrapper = () => {
  const location = useLocation();
  const pathname = location.pathname;

  const segments = useMemo(
    () => pathname.split("/").filter(Boolean),
    [pathname]
  );

  const { activeModule, activeRoute } = useMemo(() => {
    let repoIndex = segments.indexOf("r");
    let moduleSegment =
      repoIndex !== -1 ? segments[repoIndex + 2] : segments[1];

    const moduleKey = Object.keys(MODULE_DEFINITIONS).find(
      (key) => key === moduleSegment
    );

    if (!moduleKey) return {};

    const moduleDef = MODULE_DEFINITIONS[moduleKey];

    const relativeSegments =
      repoIndex !== -1
        ? segments.slice(repoIndex + 3)
        : segments.slice(2);

    const route = moduleDef.ui.routes.find((r) => {
      if (r.path === "" && relativeSegments.length === 0) return true;
      return matchPattern(r.path, relativeSegments);
    });

    return { activeModule: moduleDef, activeRoute: route };
  }, [segments]);

  if (!activeRoute) {
    return <ComponentRegistry.components.NotFound />;
  }
console.log("Component :", Component);

  const Component =
    ComponentRegistry.components[activeRoute.componentKey] ||
    ComponentRegistry.components.NotFound;

  return (
    <Suspense fallback={<div>Loading...</div>}>
      <Component />
    </Suspense>
  );
};

export default SmartRouteWrapper;
