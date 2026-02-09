import { runSmartDataEngine } from "../engine/smartDataEngine";
import { validateEngineConfig } from "../engine/validateEngineConfig";

function buildEngineConfig(moduleData, routeData) {
  const config = {};

  const backendKeys = routeData?.backendKeys;

  // simple API
  if (!backendKeys) {
    config.__root__ = {
      stateKey: moduleData.dataKey,
      idKey: moduleData.idKey,
      strategy: "merge"
    };
    return config;
  }

  const list = Array.isArray(backendKeys) ? backendKeys : [backendKeys];

  list.forEach(k => {
    if (typeof k === "string") {
      config[k] = {
        stateKey: k,
        strategy: "replace"
      };
    } else {
      config[k.key] = {
        stateKey: k.sourceKey || k.key,
        idKey: k.idKey,
        strategy: k.strategy || "merge"
      };
    }
  });

  validateEngineConfig(config);
  return config;
}

export function createGenericSlice(set, get, module) {
  const { data, ui } = module;
  const listKey = data.dataKey;
  const readyKey = data.readyKey;

  const state = {
    [listKey]: [],
    [readyKey]: false,
    syncTimestamps: {},
    [`${listKey}_lastParams`]: null,
    [`${listKey}_error`]: null
  };

  const actions = {
    [`fetch${data.name}`]: async (params) => {
      const s = get();
      const effectiveParams = params ?? s[`${listKey}_lastParams`] ?? {};

      const payload = {
        Params: effectiveParams,
        Timestamps: s.syncTimestamps
      };

      const engineConfig = buildEngineConfig(data);

      const { updates, meta } = await runSmartDataEngine({
        api: data.api,
        payload,
        state: s,
        config: engineConfig
      });

      set({
        ...updates,
        ...meta,
        [readyKey]: true,
        [`${listKey}_lastParams`]: effectiveParams
      });
    }
  };

  /* route fetchers */
  ui?.routes?.forEach(route => {
    if (!route.data?.fetcherName) return;

    actions[route.data.fetcherName] = async (params) => {
      const s = get();
      const payload = {
        Params: params ?? {},
        Timestamps: s.syncTimestamps
      };

      const engineConfig = buildEngineConfig(data, route.data);

      const { updates, meta } = await runSmartDataEngine({
        api: route.data.api,
        payload,
        state: s,
        config: engineConfig
      });

      set({ ...updates, ...meta });
    };
  });

  return { ...state, ...actions };
}
