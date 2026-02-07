/******************************************************************
 * SMART DATA ENGINE
 * ---------------------------------------------------------------
 * Responsibilities:
 *  - Normalize ANY API response shape
 *  - Apply config-driven merge / replace logic
 *  - Handle partial failures safely
 *  - Maintain param & timestamp memory
 *  - Produce state patches (NO side effects)
 ******************************************************************/

/* ===================== HELPERS ===================== */

function isPlainObject(v) {
  return v && typeof v === "object" && !Array.isArray(v);
}

/**
 * Merge array by id (UPSERT)
 */
function mergeList(current = [], incoming = [], idKey) {
  if (!idKey) return incoming;
  if (!Array.isArray(current)) return incoming;
  if (!Array.isArray(incoming)) return current;

  const map = new Map(current.map(i => [i[idKey], i]));
  incoming.forEach(item => {
    map.set(item[idKey], { ...map.get(item[idKey]), ...item });
  });
  return Array.from(map.values());
}

/**
 * Normalize ANY response into a standard internal format
 */
function normalizeResponse(raw) {
  // Case 1: array response
  if (Array.isArray(raw)) {
    return {
      type: "array",
      entries: {
        __root__: { ok: true, data: raw }
      }
    };
  }

  // Case 2: plain object (single object response)
  if (isPlainObject(raw) && !Object.values(raw).some(v => isPlainObject(v))) {
    return {
      type: "object",
      entries: {
        __root__: { ok: true, data: raw }
      }
    };
  }

  // Case 3: aggregated response
  const entries = {};
  Object.keys(raw || {}).forEach(key => {
    const item = raw[key] || {};
    entries[key] = {
      ok: item.ok !== false,
      data: item.Data ?? item.data ?? null,
      error: item.error ?? null,
      lastSyncTime: item.LastSyncTime ?? null
    };
  });

  return { type: "map", entries };
}

/* ===================== ENGINE ===================== */

/**
 * @param {Object} args
 * @param {Function} args.api               API function
 * @param {Object} args.state               current store state
 * @param {Object} args.config              engine config
 * @param {Object} args.payload             runtime params
 */
export async function runSmartDataEngine({
  api,
  state,
  config,
  payload
}) {
  const result = await api(payload);
  const normalized = normalizeResponse(result);

  const updates = {};
  const meta = {
    syncTimestamps: { ...(state.syncTimestamps || {}) }
  };

  const processEntry = (key, entry, conf) => {
    const targetKey = conf.stateKey || key;

    // ❌ Failure must NEVER wipe data
    if (!entry.ok) {
      meta[`${targetKey}_error`] = entry.error;
      meta[`${targetKey}_status`] = conf.status ?? 500;
      return;
    }

    const current = state[targetKey];
    const incoming = entry.data;

    if (conf.strategy === "merge" && Array.isArray(incoming)) {
      updates[targetKey] = mergeList(
        current || [],
        incoming,
        conf.idKey
      );
    } else {
      updates[targetKey] = incoming;
    }

    if (entry.lastSyncTime) {
      meta.syncTimestamps[key] = entry.lastSyncTime;
    }

    meta[`${targetKey}_error`] = null;
    meta[`${targetKey}_status`] = 200;
  };

  /* ---------- ROOT RESPONSE ---------- */
  if (normalized.entries.__root__) {
    const conf = config.__root__ || {
      stateKey: config.dataKey,
      strategy: "replace",
      idKey: config.idKey
    };
    processEntry("__root__", normalized.entries.__root__, conf);
  }

  /* ---------- MULTI KEY RESPONSE ---------- */
  Object.entries(normalized.entries).forEach(([key, entry]) => {
    if (key === "__root__") return;
    const conf = config[key];
    if (!conf) return;
    processEntry(key, entry, conf);
  });

  return { updates, meta };
}
