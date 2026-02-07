export function validateEngineConfig(config) {
  Object.values(config).forEach(conf => {
    if (!conf.stateKey) {
      throw new Error("EngineConfig: stateKey is required");
    }
    if (conf.strategy && !["merge", "replace"].includes(conf.strategy)) {
      throw new Error(`Invalid strategy: ${conf.strategy}`);
    }
  });
}
