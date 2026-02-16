import { create } from "zustand";
import { useOpportunityApiStore } from "./useOpportunityApiStore";
import { validateValue } from "../Shared/validationEngine";

export const useOpportunityFormStore = create((set, get) => ({
    values: {},
    errors: {},
    fields: [],
    context: {},

    setField: (fields) => set({ fields }),

    setValue: (key, value, meta = {}) => {
        const { values, errors, context, fields } = get();
        const field = fields.find(f => f.name === key);
        if (!field) return;

        let nextValues = { ...values };
        let nextErrors = { ...errors };
        let nextContext = { ...context };

        /* =====================
           CLEAR CASE
           ===================== */
        if (meta.cleared) {
            nextValues[key] = "";

            // clear context only if config allows
            if (field.clearContext === true) {
                nextContext[key] = "";
            }

            delete nextErrors[key];

            set({
                values: nextValues,
                errors: nextErrors,
                context: nextContext
            });
            return;
        }

        /* =====================
           VALIDATION
           ===================== */
        const { value: validatedValue, error } =
            validateValue(field, value);

        if (error) {
            nextErrors[key] = error;
            set({ errors: nextErrors });
            return;
        }

        delete nextErrors[key];
console.log("validatedValue :", {validatedValue, key, value, meta});

        /* =====================
           SET VALUE
           ===================== */
        nextValues[key] = validatedValue;
        nextContext[key] = validatedValue;

        /* =====================
           APPLY EFFECTS
           ===================== */
        if (field.effects && meta.raw) {
            Object.entries(field.effects).forEach(([target, path]) => {
                nextContext[target] = path
                    .split(".")
                    .reduce((acc, k) => acc?.[k], meta.raw.meta);
console.log("target :", target, values);
 
                // also mirror to values if that field exists in UI
                if (fields.some(f => f.name === target)) {
                    nextValues[target] = meta.raw.meta[target];
                }
            });
        }
console.log("nextValues :", nextValues);

        set({
            values: nextValues,
            errors: nextErrors,
            context: nextContext
        });
    }
}));