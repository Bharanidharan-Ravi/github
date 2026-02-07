//   const executeApiCall = async (apiConfig, params) => {
//     const executor = get()._apiExecutor;

//     if (!executor) {
//       throw new Error("API executor not found in store");
//     }

//     const resolved =
//       typeof apiConfig === "function" ? apiConfig(params) : apiConfig;

//     return executor({
//       url: resolved.url,
//       method: resolved.method,
//       payload: resolved.payload,
//       params,
//     });
//   };

//   // HELPER 1: Merge Logic (Safe Upsert)
// // Updates existing items by ID (merging properties), appends truly new ones
// const mergeList = (currentList, newItems, KeyField) => {
//   // 1. Safety Checks
//   if (!Array.isArray(newItems) || newItems.length === 0) return currentList;
//   if (!Array.isArray(currentList) || currentList.length === 0) return newItems;
  
//   // CRITICAL: If no KeyField is provided, we can't match rows, so we append.
//   if (!KeyField) {
//       console.warn("mergeList called without KeyField. Appending data instead of merging.");
//       return [...currentList, ...newItems]; 
//   }

//   // 2. Map new items for O(1) lookup
//   const newMap = new Map(newItems.map((item) => [item[KeyField], item]));

//   // 3. Update existing items in place (Preserve Order + Merge Properties)
//   const updatedList = currentList.map((oldItem) => {
//     const newItem = newMap.get(oldItem[KeyField]);
//     // [FIX] If match found: Merge new properties ON TOP of old ones.
//     // This prevents wiping out fields that aren't in the update payload.
//     return newItem ? { ...oldItem, ...newItem } : oldItem;
//   });

//   // 4. Find truly new items (that didn't exist in currentList)
//   const existingIds = new Set(currentList.map((item) => item[KeyField]));
//   const trulyNewItems = newItems.filter((item) => !existingIds.has(item[KeyField]));

//   return [...updatedList, ...trulyNewItems];
// };

// // HELPER 2: Normalize Keys (Optional passthrough)
// const normalizeToPascalCase = (data) => data; 

// export const CreateGenericSlice = (set, get, config) => {
//   const { name, listKey, initKey, fetcherName, api, idKey, sharedKey } = config.data;
//   const { defaults } = config.filter || {};
//   const { initialValues } = config.form || {};

//   const capitalizedName = name.charAt(0).toUpperCase() + name.slice(1);
//   const setterName = `set${capitalizedName}`;

//   // ---------------------------------------------------------
//   // CORE SMART FETCH LOGIC
//   // ---------------------------------------------------------
//   const performSmartFetch = async (
//     configKeys,     // Array: ["List"] OR [{key, sourceKey, idKey...}]
//     staticParams,
//     runTimeParams,
//     apiFunc,
//     dataKey,
//     isMainList
//   ) => {
//     const state = get();
    
//     // 1. CALL LOCKING
//     if (state[`${dataKey}_loading`]) {
//       console.warn(`[${name}] Blocked duplicate fetch for ${dataKey}`);
//       return;
//     }
//     set({ [`${dataKey}_loading`]: true });

//     // 2. PARSE CONFIGURATION (Standardize inputs)
//     const storedTimestamps = state.syncTimestamps || {};
//     const payloadTimestamps = {};
//     const configMap = {}; 

//     // Convert configKeys array into a usable Map
//     (configKeys || []).forEach((conf) => {
//       const isObj = typeof conf === 'object';
//       const key = isObj ? conf.key : conf;
//       const sourceKey = isObj ? (conf.sourceKey || conf.key) : conf;
      
//       configMap[sourceKey] = {
//         key: key,
//         sourceKey: sourceKey,
//         merge: isObj ? conf.merge : undefined,
//         idKey: isObj ? conf.idKey : undefined // <--- Capture specific ID here!
//       };

//       if (storedTimestamps[key]) {
//         payloadTimestamps[key] = storedTimestamps[key];
//       }
//     });

//     // 3. PREPARE PARAMS
//     const lastUsedParams = state[`${dataKey}_lastParams`] || {};
//     const effectiveRuntimeParams = runTimeParams || lastUsedParams;
//     const finalParams = { ...staticParams, ...effectiveRuntimeParams };

//     try {
//       // 4. EXECUTE API
//       let res;
//       if (configKeys && configKeys.length > 0) {
//         const payload = {
//           // Backend needs Source Keys (API Names)
//           ConfigKeys: Object.keys(configMap), 
//           Timestamps: payloadTimestamps,
//           Params: finalParams,
//         };
//         const finalApi = apiFunc || api; 
//         const response = await executeApiCall(api, payload);
//         res = await finalApi(payload);
//       } else {
//         const finalApi = apiFunc || api;
//         res = await (finalApi ? finalApi(finalParams) : Promise.resolve(null));
//       }

//       const responseData = res?.data || res;

//       // RELEASE LOCK IF NO DATA
//       if (!responseData) {
//          set({ [`${dataKey}_loading`]: false });
//          return;
//       }

//       // 5. PROCESS RESPONSE
//       const newTimestamps = { ...storedTimestamps };
//       const updates = {};
//       updates[dataKey] = state[dataKey]; 
//       let hasChanges = false;

//       // SCENARIO A: Dictionary Response (e.g. { "repositories": [...], "Employess": [...] })
//       if (typeof responseData === "object" && !Array.isArray(responseData)) {
        
//         Object.keys(responseData).forEach((responseKey) => {
//           const item = responseData[responseKey];

//           // Lookup Config by the key returned from API
//           const specificConfig = configMap[responseKey] || {};
          
//           // A. Resolve Target Key
//           const targetStateKey = specificConfig.key || responseKey;
          
//           // B. Resolve ID Key (CRITICAL FIX)
//           // Priority: 1. Specific Config ID (Repo_Id) -> 2. Module Default ID (Ticket_id)
//           const specificIdKey = specificConfig.idKey || idKey;
          
//           // C. Resolve Merge Strategy
//           const useMerge = specificConfig.merge ?? isMainList;

//           // Update Timestamp
//           if (item?.LastSyncTime) {
//             newTimestamps[targetStateKey] = item.LastSyncTime;
//           }

//           // Update Data
//           if (item?.Data) {
//             hasChanges = true;
//             const incomingData = normalizeToPascalCase(item.Data);
            
//             if (updates[targetStateKey] === undefined) {
//                updates[targetStateKey] = state[targetStateKey] || [];
//             }
//             const currentTargetData = updates[targetStateKey];

//             if (Array.isArray(incomingData)) {
//               if (useMerge && specificIdKey) {
//                 // [STRATEGY: SMART MERGE]
//                 // Passes 'specificIdKey' (e.g., Repo_Id) so we don't mix up Tickets and Repos
//                 updates[targetStateKey] = mergeList(currentTargetData, incomingData, specificIdKey);
//               } else {
//                 // [STRATEGY: APPEND/REPLACE]
//                 updates[targetStateKey] = [...(currentTargetData || []), ...incomingData];
//               }
//             } else {
//               // [STRATEGY: OBJECT MERGE]
//               updates[targetStateKey] = { ...(currentTargetData || {}), ...incomingData };
//             }
//           } 
//           // Handle "Clear Data" (null/missing data in Replace Mode)
//           else if (useMerge === false && !item.Data) {
//               console.log(`[${name}] Clearing data for ${targetStateKey}`);
//               updates[targetStateKey] = [];
//               hasChanges = true;
//           }
//         });
//       } 
//       // SCENARIO B: Flat Array Response
//       else if (Array.isArray(responseData)) {
//          updates[dataKey] = normalizeToPascalCase(responseData);
//          hasChanges = true;
//       }

//       // 6. COMMIT TO STORE
//       if (hasChanges || !state[dataKey]) {
//         set({
//           ...updates,
//           [`${dataKey}_lastParams`]: effectiveRuntimeParams,
//           syncTimestamps: newTimestamps,
//           ...(isMainList ? { [initKey]: true } : {}),
//           [`${dataKey}_loading`]: false 
//         });
//       } else {
//         set({ 
//           syncTimestamps: newTimestamps,
//           [`${dataKey}_loading`]: false 
//         });
//       }

//     } catch (e) {
//       console.error(`[${name}] smartFetch failed for ${dataKey}`, e);
//       set({ [`${dataKey}_loading`]: false });
//     }
//   };

//   // ---------------------------------------------------------
//   // ACTIONS GENERATION
//   // ---------------------------------------------------------

//   const mainActions = {
//     [fetcherName]: async (runTimeParams) => {
//       await performSmartFetch(
//         config.data.configKeys, 
//         config.data.params,
//         runTimeParams,
//         config.data.api,        
//         listKey,
//         true 
//       );
//     },
//   };

//   const routeActions = {};
//   if (config.ui && config.ui.routes) {
//     config.ui.routes.forEach((route) => {
//       if (route.fetcherName) {
//         routeActions[route.fetcherName] = async (runTimeParams) => {
//           const routeDataKey = `${route.key}Data`; 
          
//           // [INSTANT UI RESET]
//           set((state) => {
//              const resetUpdates = {};
//              // Always reset the page container
//              resetUpdates[routeDataKey] = {}; 

//              // Conditionally reset dependencies if NOT merging
//              if (route.configKeys) {
//                  (route.configKeys || []).forEach(conf => {
//                      const isObj = typeof conf === 'object';
//                      const key = isObj ? (conf.key || conf.sourceKey) : conf;
//                      const shouldMerge = isObj ? conf.merge : false; 

//                      // Only clear if we are in "Replace Mode" (merge: false)
//                      if (shouldMerge === false) {
//                          resetUpdates[key] = []; 
//                      }
//                  });
//              }
//              return resetUpdates;
//           });

//           await performSmartFetch(
//             route.configKeys,      
//             route.params,
//             runTimeParams,
//             route.api,             
//             routeDataKey,
//             false 
//           );
//         };
//       }
//     });
//   }

//   const initFuncName = `init${capitalizedName}Subscription`;

//   return {
//     // State Defaults
//     [listKey]: [], 
//     [sharedKey]: initialValues || {}, 
//     [`${name}Filters`]: { ...defaults },
    
//     // Loading Flags
//     [`${listKey}_loading`]: false,
//     [`${name}Data_loading`]: false,

//     ...mainActions,
//     ...routeActions,

//     [initFuncName]: () => {
//       const unsubscribe = get().subscribeToEvents?.(config.data.eventPrefix); 
//       return unsubscribe;
//     },

//     [setterName]: (data) => set({ [listKey]: data }),
//     setFilter: (newFilters) => 
//         set((state) => ({ [`${name}Filters`]: { ...state[`${name}Filters`], ...newFilters } })),
//   };
// };