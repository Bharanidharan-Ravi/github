import { useRepoStore } from "../RepoStore/RepoStore.js";

export const RepoFieldConfig = () => {
    const { clientMaster = {} } = useRepoStore() || {};
    const RepoFields = [
        { label: "Title", name: "title", type: "text", required: true, ApiValue: "Title" },
        { label: "Repo Code", name: "RepoCode", type: "text", required: true, ApiValue: "Repo_Code" },
        // { label: "Client", name: "client", required: true, type: "select", options: clientMaster?.map(langs => ({
        //         label: langs.client_Name,
        //         value: { id: langs.client_Id, name: langs.client_Name },
        //     })), isMulti: false, ApiValue: "Client_Id"
        // },
        { label: "Client Name", name: "ClientName", type: "text", required: true, ApiValue: "Client_Name" },
        { label: "Client Code", name: "clientCode", type: "text", required: true, ApiValue: "Client_Code" },
        { label: "Username", name: "Username", type: "text", required: true, ApiValue: "Username" },
        { label: "Password", name: "Password", type: "text", required: true, ApiValue: "Password" },
        { label: "Mail Id", name: "mailId", type: "text", required: true, ApiValue: "mailId" , isMulti: true, disabled: true },
        { label: "ValidFrom", name: "ValidFrom", type: "date", required: true, ApiValue: "Valid_From" },
        { label: "Description", name: "description", type: "CommentBar", required: true, ApiValue: "Description" },
        // { 
        //     label: "Enable Client Select",
        //     name: "clientToggle",
        //     type: "button",
        //     toggle: true,
        //     defaultValue: false ,
        //     ApiValue: "clientToggle"
        // },
    ];
    return { RepoFields }
}