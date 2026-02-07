import React, { useEffect } from "react";
import { useCustomStore, getProject } from "shared-store";
import { Link, useLocation, useNavigate } from "react-router-dom";
import {  addEventHandler } from "shared-signalr";
import SearchDiv from "../../../Shared/searchDiv/searchDiv";
import { FilterToolbar } from "../../../Shared/FilterToolbar/FilterToolbar";
import { ChevronRight } from "lucide-react";
import { Folder } from "lucide-react";
import { projectConfig } from "../Modals/ProjectModals";
import { useProjStore } from "../Store/ProjectStore";

const ViewProject = () => {
    const { projectMaster, searchTerm,filters, setFilters, setProject, updateProject, setSearchTerm,
        reSetForm, filterData
    } = useProjStore();
    const { addUpdateProj } = useCustomStore.getState();
    const Project = useCustomStore(state => state.masterProject);
    const filterList = ["status", "sort"];
    const navigate = useNavigate();

    const fetchData = async () => {
        const result = await getProject({});
        setProject(result);
    };

    useEffect(() => {
        // const onCreated = (ticket) => {
        //     console.log("Project Created: ", ticket, filters);
        //     updateProject(ticket);
        //     addUpdateProj(ticket);
        // }
        // const onUpdated = (ticket) => {
        //     console.log("Ticket Created: ", ticket);
        // }
        // const onDeleted = (ticket) => {
        //     console.log("Ticket Created: ", ticket);
        // }
        // addEventHandler("ProjCreated", onCreated);
        // addEventHandler("TicketUpdated", onUpdated);
        // addEventHandler("TicketDeleted", onDeleted);
        // fetchData();
        // return () => {
        //     removeEvenHandler("ProjCreated", onCreated);
        //     removeEvenHandler("TicketUpdated", onUpdated);
        //     removeEvenHandler("TicketDeleted", onDeleted);
        // }
    }, []);

    const openModal = () => {
        navigate('create');
    }

    return (
        <div className="container">
            <div className="repo-header">
                <SearchDiv
                    searchValue={searchTerm}
                    onChange={setSearchTerm}
                    openModal={openModal}
                    placeholder={"Projects"}
                    masterData={projectMaster}
                    onSearchResult={setProject}
                    searcFields={["project_Name"]}
                    parentData={Project}
                />
                <FilterToolbar onChange={setFilters} filterData={filterData}
                    filterList={filterList}
                    filters={filters}
                    multiSelect={{
                        labels: true,
                    }}
                    masterData={Project}
                    parentdata={Project}
                    setShowValue={setProject} />
            </div>

            <div className="repo-body">
                <ul className="ticket-list">
                    {projectMaster?.map((proj) => (
                        <li key={proj.id} className={`ticket ${proj.status}`}  >
                            <Link to={`${proj.id}`} className="ticket-link">
                                <div className="ticket-header">
                                    <div>
                                        {<Folder size={24} />}
                                        <span className="Repo-title">{proj.project_Name}</span>
                                    </div>
                                    <div>
                                        <span>{proj.status}</span>
                                        <ChevronRight className="repo-arrow" />
                                    </div>
                                </div>
                                <div className="list-body">
                                    <span>{proj.employeeName}</span>
                                    <span>{proj.dueDate}</span>
                                </div>
                            </Link>
                        </li>
                    ))}
                </ul>
            </div>
        </div>
    );
};

export default ViewProject;