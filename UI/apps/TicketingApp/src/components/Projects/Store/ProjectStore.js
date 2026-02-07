import { create } from "zustand";
import { filterEngine } from "../../../Shared/FilterToolbar/FilterToolbar";
import { useRepoStore } from "../../Repository/RepoStore/RepoStore";

export const useProjStore = create((set, get) => ({
    projectMaster: [],
    searchTerm: [],
    employeeMaster: [],
    projectDetails: [],
    formData: {
        Title: "",
        Description: "",
        ProjCode: "",
        Client_Id: "",
        Client_Name: "",
        Repo_Name: "",
        Repo_Id: "",
    },
    filterData: {
        status: ["all", "Active", "Closed"],
        sort: {
          "Newest": { field: "dueDate", order: "desc" },
          "Oldest": { field: "dueDate", order: "asc" },
          "A -> Z": { field: "project_Name", order: "asc" },
          "Z -> A": { field: "project_Name", order: "desc" }
        }
    },
    showForm: false,
    formErrors: [],
    filters: { status: "all", sort: "Newest" },

    setEmployee: (employee) => set({employeeMaster: employee}),

    setFilters: (newFilters) => set({ filters: { ...get().filters, ...newFilters } }),

    setProject: (data) => set({ projectMaster: data }),

    setSearchTerm: (searchTerm) => set({ searchTerm }),

    setProjectDetails: (data) => set({ projectDetails: data }),

    updateProject: (proj) => {
        // Get current filters from state
        const { filters, projectMaster, filterData } = get();

        // Append the new repo
        const updatedList = [...projectMaster, proj];

        // Apply filter and sort logic
        const newList = filterEngine({ labels: true }, updatedList, filters, filterData);
        return set({ projectMaster: newList });
    },

    handleInputChange: (label, value) => {
        let mappedValue = value;
        set((state) => ({
            formData: {
                ...state.formData,
                [label]: mappedValue,
            },
        }))
    },

    setShowForm: (show) => set({ showForm: show }),

    closeModal: () => set({ showForm: false }),

    reSetForm: () => set({
        formData: {
            Title: "",
            Description: "",
            ProjCode: "",
            Client_Id: "",
            Client_Name: "",
            Repo_Name: "",
            Repo_Id: "",
        }
    })

}));

