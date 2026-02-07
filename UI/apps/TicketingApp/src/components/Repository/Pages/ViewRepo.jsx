import { useEffect, useLayoutEffect, useRef, useState } from "react";
import "../ViewRepo.css";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { useTicketStore } from "../../Tickets/TicketStore/TicketStore.js";
import DynamicForm from "../../../Shared/DynamicForm/DynamicForm.jsx";
import {  addEventHandler } from "shared-signalr";
import { RepoFieldConfig } from "../RepoModal/RepoModal.js";
import { useCustomStore, GetClientMaster, postRepo, GetAllRepo } from "shared-store";
import { useRepoStore } from "../RepoStore/RepoStore.js";
import { Folder, ChevronRight } from "lucide-react";
import SearchDiv from "../../../Shared/searchDiv/searchDiv.jsx";
import { FilterToolbar } from "../../../Shared/FilterToolbar/FilterToolbar.jsx";
import Modal from "../../../Shared/Modal/Modal.jsx";

const ViewRepo = () => {
    const navigate = useNavigate();
    const isStandalone = !window.__MICRO_FRONTEND__;
    const { RepoFields } = RepoFieldConfig();
    const Repo = useCustomStore(state => state.repositories);
    // const clients = useCustomStore(state => state.clients);
    const { addUpdateRepo } = useCustomStore.getState();
    const { clientMaster, RepoMaster, searchTerm, showForm,
        formData, filters, createdRepo, setCreatedRepo,
        setFilters, setSearchTerm, setClientMaster, setRepoMaster,
        updateRepo, handleInputChange, setShowForm,
        reSetForm, formErrors, filterData } = useRepoStore();
    const [showSuccessModal, setShowSuccessModal] = useState(false);
    const filterList = ["status", "sort"];
    const location = useLocation();
    console.log("repo t:", Repo);
    
    const fetchData = async () => {
        if (Repo && Repo.length > 0) {
            setRepoMaster(Repo);
        }
        //  else {
        //     const result = await GetAllRepo();
        //     setRepoMaster(result);
        // }
    };

    // useEffect(() => {
    //     const onCreated = (ticket) => {
    //         console.log("repo Created: ", ticket, filters);
    //         updateRepo(ticket);
    //         addUpdateRepo(ticket);
    //     }
    //     const onUpdated = (ticket) => {
    //         console.log("Ticket Created: ", ticket);
    //     }
    //     const onDeleted = (ticket) => {
    //         console.log("Ticket Created: ", ticket);
    //     }
    //     addEventHandler("RepoCreated", onCreated);
    //     addEventHandler("TicketUpdated", onUpdated);
    //     addEventHandler("TicketDeleted", onDeleted);
    //     fetchData();
    //     return () => {
    //         removeEvenHandler("RepoCreated", onCreated);
    //         removeEvenHandler("TicketUpdated", onUpdated);
    //         removeEvenHandler("TicketDeleted", onDeleted);
    //     }
    // }, []);

    useEffect(() => {
        const previousPath = sessionStorage.getItem('previousPath');
        if (previousPath && location.pathname === previousPath) {
            setShowForm(false);
            sessionStorage.removeItem('previousPath');

            const basePath = location.pathname.split('/').slice(0, 2).join('/');
            const fullPath = isStandalone ? '/repository' : `${basePath}/repository`;

            // allow paint before navigating
            requestAnimationFrame(() => {
                navigate(fullPath, { replace: true });
            });
        } else if (location.pathname.includes('create')) {
            setShowForm(true);
        } else {
            setShowForm(false);
        }
    }, [location.pathname]);


    const openModal = () => {
        setShowForm(true);
        navigate('create');
    }

    const handleClose = () => {
        setShowForm(false);
        navigate(-1);
    }

    const handleSubmit = async (data) => {
        const mailList = (data.mailId || []).map(m => ({
            mailIds: m
        }));

        const payload = {
            login: {
                userName: data.Username,
                password: data.Password,
                role: 3,
                dbName: "WG_APP"
            },
            client: {
                clientCode: data.Client_Code,
                clientName: data.Client_Name,
                description: data.Description,  // shared field
                valid_From: data.Valid_From,
                valid_To: null,
                clientsmailids: mailList
            },
            repo: {
                title: data.Title,
                description: data.Description,   // shared field
                repo_Code: data.Repo_Code,
                client_Id: null,
                created_On: null,
                status: "Active"
            }
        };
        try {
            const result = await postRepo(payload);

            // ALSO persist for page reloads
            sessionStorage.setItem("createdRepo", JSON.stringify(result));
            setShowSuccessModal(true)
        } catch (error) {
            console.error("Error posting repo:", error);
        }
    };

    const handleModalClose = () => {
        sessionStorage.removeItem("createdRepo");
        setShowSuccessModal(false);
        reSetForm();
        setShowForm(false);
        navigate(-1);
    };

    const handleNavigateSmooth = () => {
        // Get the current base path (e.g., /employee or /admin)
        const basePath = window.location.pathname.split('/').slice(0, 2).join('/');  // /employee or /admin

        // Define the relative path to navigate to
        const relativePath = '/projects/create';
        let fullPath
        if (isStandalone) {

            fullPath = '/projects/create'
        } else {
            fullPath = `${basePath}${relativePath}`;
        }
        // Combine the base path with the relative path
        sessionStorage.setItem('previousPath', location.pathname);
        // Navigate to the full URL
        navigate(fullPath);

        // Optional: Close modal and perform other actions after navigation
        setShowSuccessModal(false);
        setShowForm(false);
        reSetForm();
    };

    return (
        <div className="tickets-container container">
            {!showForm ?
                <>
                    <div className="repo-header">
                        <SearchDiv
                            searchValue={searchTerm}
                            onChange={setSearchTerm}
                            openModal={openModal}
                            placeholder={"Repository"}
                            masterData={RepoMaster}
                            onSearchResult={setRepoMaster}
                            searcFields={["title"]}
                            parentData={Repo}
                        />
                        <FilterToolbar onChange={setFilters} filterData={filterData}
                            filterList={filterList}
                            filters={filters}
                            multiSelect={{
                                labels: true,
                            }}
                            masterData={RepoMaster}
                            parentdata={Repo}
                            setShowValue={setRepoMaster} />
                    </div>

                    <div className="repo-body">
                        <ul className="ticket-list">
                            {RepoMaster?.map((Repo) => (
                                <li key={Repo.repo_Id} className={`ticket ${Repo.status}`}  >
                                    <Link to={`${Repo.repo_Id}`} className="ticket-link">
                                        <div className="ticket-header">
                                            <span className="Repo-title">{<Folder />}{Repo.title}</span>
                                            <ChevronRight className="repo-arrow" />
                                        </div>
                                    </Link>
                                </li>
                            ))}
                        </ul>
                    </div>
                </>
                :
                <DynamicForm
                    labels={RepoFields}
                    formData={formData}
                    formErrors={formErrors}
                    onChange={(label, value) => handleInputChange(label, value)}
                    onSubmit={handleSubmit}
                    // useInputField={true}
                    onClose={handleClose}
                    clientMaster={clientMaster}
                // submitDisabled={submitDisabled}
                />}
            <Modal title="Repository Created!" isOpen={showSuccessModal}
                onClose={handleModalClose}>
                <p>Would you like to create projects for this repository?</p>
                <div className="modal-buttons">
                    <button
                        onClick={handleNavigateSmooth}
                    >
                        Yes
                    </button>
                    <button onClick={handleModalClose}>No</button>
                </div>
            </Modal>

        </div>
    );
};

export default ViewRepo;