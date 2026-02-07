import { useRepoStore } from "../../Repository/RepoStore/RepoStore.js";
import { useProjStore } from "../Store/ProjectStore.js";

export const projectConfig = () => {
    const { RepoMaster = {} } = useRepoStore() || {};
    const { employeeMaster = {} } = useProjStore() || {};
    const ProjectFields = [
        { label: "Title", name: "title", type: "text", required: true, ApiValue: "Title" },
        { label: "Repository", name: "Repository", required: true, type: "select",
             options: RepoMaster?.map(langs => ({
                label: langs.title,
                value: { id: langs.repo_Id, name: langs.title },
            })), isMulti: false, ApiValue: "Repository"
        },
        { label: "Project Code", name: "ProjCode", type: "text", required: true, ApiValue: "ProjCode" },
        // { label: "Repo Name", name: "RepoName", type: "text", required: true, ApiValue: "RepoName" },
        { label: "Responsible", name: "Responsible", type: "select", required: true, 
            options: employeeMaster?.map(langs => ({
            label: langs.UserName,
            value: { id: langs.UserID, name: langs.UserName },
        })),ApiValue: "Responsible" },
        { label: "Due Date", name: "DueDate", type: "date", required: true, ApiValue: "DueDate" },        
        { label: "Description", name: "description", type: "textarea", required: true, ApiValue: "Description" },
    ];
    return { ProjectFields }
}