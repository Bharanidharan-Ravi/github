export const createMasterDataSlice = (set, get) => ({
    clients: null,
    Employess: null,
    labelMaster: null,

    setEmployess: ((Employee) => set({Employess: Employee})),     
    setClientMaster: (clients) => set({clients}),
    setLabelMaster: (label) => set({labelMaster: label}),
});