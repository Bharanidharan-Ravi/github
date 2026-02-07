import { useDynamicSync } from "./useDynamicSync";
import { SmartRouteWrapper } from "./SmartRouteWrapper";

export function AppRuntime({ modules }) {
  useDynamicSync(modules);
  return <SmartRouteWrapper />;
}
