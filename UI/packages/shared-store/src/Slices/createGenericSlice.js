export const createGenericSlice = (set, get, config) => {
    const { name, listKey, initKey, fetcherName, api, idKey } = config.data;

    return {
        [listKey]: null,
        [initKey]: false,

        [fetcherName]: async () => {
            const state = get();
            
            // ====================================================
            // SCENARIO 1: First Load (Show Spinner)
            // ====================================================
            if (!state[initKey]) {
                console.log(`⬇️ [${name}] Initial Fetch...`);
                try {
                    // Call API without arguments.
                    // Result: _silent is undefined. Interceptor SHOWS spinner.
                    const res = await api(); 
                    
                    const data = res?.data || res; 
                    set({ [listKey]: data, [initKey]: true });
                } catch (e) { console.error(e); }
                return;
            }

            // ====================================================
            // SCENARIO 2: Background Refresh (Hide Spinner)
            // ====================================================
            console.log(`🔄 [${name}] Background Sync...`);
            try {
                // Call API with the silent flag.
                // Result: _silent is true. Interceptor HIDES spinner.
                const res = await api({ _silent: true }); 
                
                const data = res?.data || res;
                set({ [listKey]: data });
            } catch (e) { 
                console.warn(`Background sync failed`, e); 
            }
        },
        
        // Actions
        [`add${name}`]: (item) => set((state) => ({
            [listKey]: [item, ...(state[listKey] || [])]
        })),
        [`update${name}`]: (item) => set((state) => ({
            [listKey]: (state[listKey] || []).map(i => i[idKey] === item[idKey] ? item : i)
        })),
        [`delete${name}`]: (id) => set((state) => ({
            [listKey]: (state[listKey] || []).filter(i => i[idKey] !== id)
        }))
    };
};