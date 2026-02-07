import React, { useEffect, useState } from "react";
import Modal from "../../../Shared/Modal/Modal";
import { useLocation, useNavigate } from "react-router-dom";
import DynamicForm from "../../../Shared/DynamicForm/DynamicForm";
import { useRepoStore } from "../RepoStore/RepoStore";
import { useCustomStore, GetClientMaster,postRepo  } from "shared-store";
import { RepoFieldConfig } from "../RepoModal/RepoModal";
import { markSkipHistory, useSkipHistory } from "../../../Shared/Hooks/useSkipHistory";

const CreateRepo = () => {
    const navigate = useNavigate();
    const location = useLocation();
    const isStandalone = !window.__MICRO_FRONTEND__;
    const [showSuccessModal, setShowSuccessModal] = useState(false);
    const { RepoFields } = RepoFieldConfig();
    const basePath = location.pathname.split('/').slice(0, 2).join('/');
    const fullPath = isStandalone ? '/repository' : `${basePath}/repository`;
    const { formData, formErrors, handleInputChange,
        clientMaster, setClientMaster, reSetForm
    } = useRepoStore();
    const clients = useCustomStore(state => state.clients);
    const fetchData = async () => {
        if (clients && clients.length > 0) {
            setClientMaster(clients);

        } else {
            const result = await GetClientMaster();
            setClientMaster(result);
        }
    }
    useSkipHistory(fullPath);

    useEffect(() => {
        fetchData();
    }, []);

    const handleModalClose = () => {
        sessionStorage.removeItem("createdRepo");
        setShowSuccessModal(false);
        reSetForm();
        // setShowForm(false);
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

        markSkipHistory(location.pathname);
        // Navigate to the full URL
        navigate(fullPath);

        setShowSuccessModal(false);
        reSetForm();
    };

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

    const handleClose = () => {
        // setShowForm(false);
        navigate(-1);
    };
console.log("formdata :", formData);

    return (
        <div>
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
            />
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

export default CreateRepo;