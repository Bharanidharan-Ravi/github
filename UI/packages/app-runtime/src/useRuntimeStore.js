let _store = null;

export function initRuntimeStore(store) {
  _store = store;
}

export function useRuntimeStore(selector) {
  if (!_store) {
    throw new Error("Runtime store not initialized");
  }
  return _store(selector);
}

useRuntimeStore.getState = () => _store.getState();
