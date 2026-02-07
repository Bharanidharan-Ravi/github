import { create } from "zustand";
import { useCustomStore, postRepo } from "shared-store";
import { filterEngine } from "../../../Shared/FilterToolbar/FilterToolbar";

export const useRepoStore = create((set, get) => ({
    clientMaster: [],
    RepoMaster: [],
    searchTerm: "",
    showForm: false,
    projMaster: [],
    repoDetails: [],
    filterData: {
        status: ["all", "Active", "Closed"],
        sort: {
          "Newest": { field: "created_On", order: "desc" },
          "Oldest": { field: "created_On", order: "asc" },
          "A -> Z": { field: "title", order: "asc" },
          "Z -> A": { field: "title", order: "desc" }
        }
    },
    formData: {
        Title: "",
        Description: "",
        Client_Id: "",
        Repo_Code: "",
        Client_Code: "",
        Client_Name: "",
        Password: "",
        Username: "",
        Valid_From: "",
        mailId: []
    },
    formErrors: {},
    filters: { status: "all", sort: "Newest" }, // move filters here
    createdRepo: null,

    setRepoDetails: (data) => set({ repoDetails:data }),

    setProjMaster: (data) => set({ projMaster: data }),

    setCreatedRepo: (data) => set({ createdRepo: data }),

    setFilters: (newFilters) => set({ filters: { ...get().filters, ...newFilters } }),

    setSearchTerm: (searchTerm) => set({ searchTerm }),

    setClientMaster: (clients) => set({ clientMaster: clients }),

    setRepoMaster: (repo) => set({ RepoMaster: repo }),

    updateRepo: (Repo) => {
        // Get current filters from state
        const { filters, RepoMaster,filterData } = get();

        // Append the new repo
        const updatedList = [...RepoMaster, Repo];

        // Apply filter and sort logic
        const newList = filterEngine({ labels: true }, updatedList, filters, filterData);
        return set({ RepoMaster: newList });
    },

    handleInputChange: (label, value) => {
        let mappedValue = value;
        // if (["Label", "Treatment", "Clinic"].includes(label)) {
        //     mappedValue = Array.isArray(value)
        //         ? value.map(item => ({
        //             label: item.name || item.label,  // Fallback to 'label' if 'name' not present
        //             value: {
        //                 id: item.id || item.value?.id || item,
        //                 name: item.name || item.label || item.value?.name || ""
        //             }
        //         }))
        //         : [];
        // };

        set((state) => ({
            formData: {
                ...state.formData,
                [label]: mappedValue,
            },
        }))
    },

    setShowForm: (show) => set({ showForm: show }),

    closeModal: (show) => set({ showForm: !show }),

    reSetForm: () => set({
        formData: {
            Title: "",
            Description: "",
            Client_Id: "",
            Repo_Code: "",
            Client_Code: "",
            Client_Name: "",
            Password: "",
            Username: "",
            Valid_From: "",
            mailId: []
        }
    })

}));