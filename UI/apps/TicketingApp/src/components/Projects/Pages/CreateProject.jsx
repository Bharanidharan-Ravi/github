import React, { useEffect } from "react";
import { projectConfig } from "../Modals/ProjectModals";
import { useProjStore } from "../Store/ProjectStore";
import { PostProject, useCustomStore, GetEmployees,GetAllRepo } from "shared-store";
import DynamicForm from "../../../Shared/DynamicForm/DynamicForm";
import { useRepoStore } from "../../Repository/RepoStore/RepoStore";
import { useNavigate } from "react-router-dom";

const ProjectCreate = () => {
    const { ProjectFields } = projectConfig();
    const { formData, formErrors, handleInputChange, reSetForm, setEmployee } = useProjStore();
    const { setRepoMaster } = useRepoStore();
    const Employee = useCustomStore(state => state.Employess);
    const Repo = useCustomStore(state => state.Repo);
    const navigate = useNavigate();
    const fetchData = async () => {
        if (Repo && Repo.length > 0) {
            setRepoMaster(Repo);
        } else {
            const result = await GetAllRepo();
            setRepoMaster(result);
        }
        if (Employee && Employee.length > 0) {
            setEmployee(Employee);
        } else {
            const result = await GetEmployees();
            setEmployee(result);
        }
    };

    useEffect(() => {
        fetchData();
    }, []);

    useEffect(() => {
        const stored = sessionStorage.getItem("createdRepo");

        if (stored) {
            const parsed = JSON.parse(stored);
            const data = { id: parsed.Repo_Id, name: parsed.Title }
            handleInputChange("Repository", data)
        }
    }, []);

    const handleClose = () => {
        sessionStorage.removeItem("createdRepo");
        reSetForm();
        navigate(-1);
    }

    const handleSubmit = async (data) => {
        var repoData = [];
        if (formData.Repository) {
            const value = data.Repository;
            repoData = Repo.find((repo) => repo.repo_Id === value.id);
        } else {
            alert("select repository before submit");
            return;
        }

        const payload = {
            title: data.Title,
            description: data.Description,
            client_Id: repoData?.client_Id,
            repo_Id: repoData?.repo_Id,
            projCode: data.projCode,
            responsible: data.Responsible.id,
            dueDate: data.DueDate,
            ProjCode: data.ProjCode
        };
        try {
            const result = await PostProject(payload);
            reSetForm();
            setShowSuccessModal(true);
            sessionStorage.removeItem("createdRepo");
        } catch (error) {
            console.error("Error posting Project:", error);
        }
    };

    return (
        <div>
            <DynamicForm
                labels={ProjectFields}
                formData={formData}
                formErrors={formErrors}
                onChange={(label, value) => handleInputChange(label, value)}
                onSubmit={handleSubmit}
                // useInputField={true}
                onClose={handleClose}
            // clientMaster={clientMaster}
            // submitDisabled={submitDisabled}
            />
        </div>
    );
};

export default ProjectCreate;