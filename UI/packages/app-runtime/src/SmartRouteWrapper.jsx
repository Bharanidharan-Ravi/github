import { Suspense } from "react";
import { useLocation } from "react-router-dom";
import { ComponentRegistry } from "./registry/ComponentRegistry";

export function SmartRouteWrapper() {
  const { pathname } = useLocation();
  const parts = pathname.split("/").filter(Boolean);

  const moduleName = parts.includes("r")
    ? parts[parts.indexOf("r") + 2]
    : parts[0];

  const Component = ComponentRegistry[moduleName];

  if (!Component) return <div>404</div>;

  return (
    <Suspense fallback="Loading...">
      <Component />
    </Suspense>
  );
}
