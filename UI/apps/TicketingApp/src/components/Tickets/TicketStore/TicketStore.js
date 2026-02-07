import { create } from "zustand";
import { useCustomStore, PostIssues,RemoveImage } from "shared-store";
import { ProjectFilter } from "../../../Shared/helper/utilies";

export const useTicketStore = create((set, get) => ({
    tickets: [],
    searchTerm: "",
    projectMaster: [],
    formData: {
        title: "",
        project: "",
        assignedTo: "",
        label: [],
        project: "",
    },
    AttachImages: [],
    showForm: false,
    formErrors: {},
    submitDisabled: false,
    threadList: [],
    threadIssuesData: [],

    setTickets: (ticketList) => set({ tickets: ticketList }),

    setSearchTerm: (searchTerm) => set({ searchTerm }),

    setProject: (data) => set({ projectMaster: data }),

    setThreadList: (data) => set({ threadList: data }),
    setThreadIssuesData: (data) => set({ threadIssuesData: data }),

    addTickets: (newTicket) =>
        set((state) => ({
            tickets: [...state.tickets, newTicket],
        })),

    addImages: (newImages) =>
        set((state) => ({
            AttachImages: [...state.AttachImages, newImages],
        })),

    setremoveImage: async (imageUrl) => {
        console.log("imageUrl :", imageUrl);
        try {
            // Prepare payload exactly as backend expects
            const Imagepayload = {
                delete: "single",
                temps: [
                    {
                        fileName: imageUrl.name,
                        publicUrl: imageUrl.tempUrl,
                        LocalPath: imageUrl.localPath   // Must match backend LocalPath
                    }
                ]
            };

            console.log("Delete payload:", Imagepayload);

            await RemoveImage(Imagepayload);
            set((state) => ({
                AttachImages: state.AttachImages.filter(img => img.tempUrl !== imageUrl.tempUrl)
            }))
        } catch {

        }
    },

    // removeInlineImage: (imageUrl) => {
    //     const { AttachImages } = useCustomStore.getState();
    //     const found = AttachImages.find(img => 
    //         img.tempUrl === imageUrl || img.finalUrl === imageUrl
    //     );


    // },
    fetchFilterTickets: (searchTerm) => {
        const { TicketMaster } = useCustomStore.getState(); // full data
        set({ searchTerm }); // store search term if needed
        const filtered = TicketMaster?.filter((t) =>
            t.title.toLowerCase().includes(searchTerm.toLowerCase())
        ) || [];
        set({ tickets: filtered }); // update tickets for UI
    },


    handleInputChange: (label, value) => {
        let mappedValue = value;
        // if (label === 'project') {
        //     const { projectMaster } = get();
        //     console.log("projectMaster :", projectMaster);
        //     // const filterProj = projectMaster.find((data) => data.id === mappedValue.id);
        //     const filterProj = projectMaster.find((data) => data.id === mappedValue.id);
        //     console.log("filterProj :", filterProj);            
        // }
        set((state) => ({
            formData: {
                ...state.formData,
                [label]: mappedValue,
            },
        }))
    },

    // handleSubmit: (data) => {
    //     console.log("handleSubmit :", data);
    //     const result = async() => {
    //         await PostIssues();
    //     }
    //     result();
    // },
    handleSubmit: async (data) => {
        const { projectMaster, AttachImages, resetForm } = get();
        const mappedValue = data.project
        const filterProj = projectMaster.find((data) => data.id === mappedValue.id);
        console.log(filterProj, data, projectMaster);

        const payload = {
            repo_Id: filterProj.repo_Id,
            title: data.title,
            description: data.description,
            // issuer_Id: "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            // created_On: "2025-11-29T10:08:41.581Z",
            // updated_On: "2025-11-29T10:08:41.581Z",
            project_Id: mappedValue.id,
            assignee_Id: data.assignedTo.id,
            due_Date: data.dueDate,
            // status: "string",
            issuelink_Id: "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            issue_Code: data.issue_Code,
            labels: (data.label || []).map(l => ({
                label_Id: l.id,
                Issue_Id: null         // or some issueId
            })),
            tempReturns: {
                delete: "all",
                temps: (AttachImages || []).map(img => ({
                    fileName: img.name,
                    publicUrl: img.url,
                    localPath: img.LocalPath
                }))
            }
        }
        console.log("payload :", payload);


        try {
            // Assuming PostIssues returns data you want to save in the store
            const result = await PostIssues(payload);
            // Example: Update the Zustand store after successful API call
            // useStore.setState({ issues: result }); 
            resetForm();
            console.log("Issue posted successfully:", result);

        } catch (error) {
            console.error("Error posting issue:", error);
        }
    },

    resetForm: () =>
        set({
            formData: {
                title: "",
                project: "",
                assignedTo: "",
                label: [],
                description: "",
                issue_Code: "",
                dueDate: null,
                // add any other fields you have in formData
            },
            AttachImages: [],
            formErrors: {},    // optional: clear form errors
        }),

    // openModal: () => {
    //     navigate('/create', {
    //         state: { backgroundLocation: location }  // Pass the current location as backgroundLocation
    //     });
    //     set({ showForm: true })
    // },
    setShowForm: (show) => set({ showForm: show }),
    closeModal: () => set({ showForm: false }),
}));