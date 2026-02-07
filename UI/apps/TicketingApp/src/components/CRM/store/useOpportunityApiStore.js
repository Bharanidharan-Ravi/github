import { create } from "zustand";
import { GetAllCustomer, GetAllReponew } from "./CRMThunk";

export const useOpportunityApiStore = create((set) => ({
    masters: {
        verticalList: [],
        stagesList: [],
        salesPersonDetails: [],
        cyclinerApplicationDatas: [],
        powerbankrApplicationDatas: [],
        frieghtList: [],
        itemCodes: [],
        customerDetails: [],
        contactPersonDetails: []
    },

    fetchMasters: async () => {
        // set({ loading: true, error: null });

        try {
            const res = await GetAllReponew();
            console.log("Response from GetAllReponew:", res);
            const customer = await GetAllCustomer();
            console.log("Response from GetAllCustomer:", customer);
            const cust = customer || {};
            const data = res || {};

            set({
                masters: {
                    verticalList: data.verticalList,
                    stagesList: data.stagesList,
                    salesPersonDetails: data.salesPersonDetails,
                    cyclinerApplicationDatas: data.cyclinerApplicationDatas,
                    powerbankrApplicationDatas: data.powerbankrApplicationDatas,
                    frieghtList: data.frieghtList,
                    itemCodes: data.itemCodes,
                    customerDetails: cust.customerMaster || [],
                    contactPersonDetails: cust.contactPersonList || []
                },
            });

            return data;
        } catch (e) {
            set({ error: e.message, loading: false });
        }
    }
}));
