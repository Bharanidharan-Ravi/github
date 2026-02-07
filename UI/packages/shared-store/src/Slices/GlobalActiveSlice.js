export const CreateActiveSlice = (set) => ({
    activeModule: null,

    setActiveModule: (moduleName) => set({ activeModule: moduleName }),
})