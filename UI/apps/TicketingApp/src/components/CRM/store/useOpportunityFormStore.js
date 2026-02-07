import { create } from "zustand";
import { useOpportunityApiStore } from "./useOpportunityApiStore";

export const useOpportunityFormStore = create((set, get) => ({
    values: {},

    setValue: (key, value) => {
        const { masters } = useOpportunityApiStore.getState();
        const newValues = { ...get().values, [key]: value };
        console.log("Setting value:", key, value, "New values:", newValues);
        
        // 🔁 Customer selected
        if (key === "customerCode") {
            const customer = masters.customerDetails.find(
                c => c.customerCode === value
            );
console.log("Found customer for code", value, ":", customer);
            if (customer) {
                newValues.customerName = customer.customerName;
                newValues.salesPerson = customer.slpCode;
                newValues.contactPerson = customer.contactPersonCode;
            }
        }

        // 🔁 Sales person selected → filter customer
        if (key === "salesPerson") {
            const customer = masters.customerDetails.find(
                c => c.salesPersonCode === value
            );

            if (customer) {
                newValues.customerCode = customer.customerCode;
                newValues.customerName = customer.customerName;
            }
        }

        set({ values: newValues });
    }
}));