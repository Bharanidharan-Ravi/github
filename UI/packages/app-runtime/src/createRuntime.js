import {
  createApiClient,
  createApiExecutor,
  createStoreFromModules,
} from "zustand-data-orchestrator";

// export function createRuntime({ modules, baseURL }) {
//   const apiClient = createApiClient({
//     baseURL,
//     getToken: () => localStorage.getItem("token"),
//   });

//   const apiExecutor = createApiExecutor(apiClient);

//   // Inject executor into modules
//   modules.forEach((m) => {
//     m.apiExecutor = apiExecutor;
//   });

//   const store = createStoreFromModules(modules);

//   return { store, modules };
// }
