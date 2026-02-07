import React, { useEffect } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useProjStore } from "../Store/ProjectStore";
import { getProject, useCustomStore, GetTicketById } from "shared-store";
import { Folder } from "lucide-react";
import { ChevronRight } from "lucide-react";

const ProjectDetails = () => {
    const isStandalone = !window.__MICRO_FRONTEND__;
    const { projId } = useParams();
    const { setProject, setProjectDetails, projectDetails } = useProjStore();
    const Project = useCustomStore(state => state.masterProject);
    const TicketsByID = useCustomStore(state => state.TicketsByID);
    const navigate = useNavigate();
    console.log("TicketsByID :", TicketsByID);

    useEffect(() => {
        const fetchData = async () => {
            if (!Project?.length) {
                const result = await getProject({});
                setProject(result);
            } else {
                setProject(Project);
            }
            if (TicketsByID?.length === 0) {
                const result = await GetTicketById({ ProjectId: projId });
                console.log("result :", result);
                // setProjMaster(result);
            }
        };
        if (!Project || Project.length === 0 || !TicketsByID || TicketsByID.length === 0) {
            fetchData();
        }
    }, []);

    useEffect(() => {
        if (Project?.length) {
            const projDetail = Project.find(
                (item) => item.id === projId
            );
            setProjectDetails(projDetail);
        }
    }, [Project, projId]);

    const handleNavigateSmooth = () => {
        const basePath = window.location.pathname.split('/').slice(0, 2).join('/');  // /employee or /admin
        const relativePath = '/tickets/create';
        let fullPath
        if (isStandalone) {

            fullPath = '/tickets/create'
        } else {
            fullPath = `${basePath}${relativePath}`;
        }
        // Navigate to the full URL
        navigate(fullPath);
    };

    const handleRepoDetail = (ticketId) => {
        console.log("its trigger ,", ticketId);

        const basePath = window.location.pathname.split('/').slice(0, 2).join('/');  // /employee or /admin
        const relativePath = `/tickets/${ticketId}`;
        let fullPath
        if (isStandalone) {

            fullPath = `/tickets/${ticketId}`
        } else {
            fullPath = `${basePath}${relativePath}`;
        }
        // Navigate to the full URL
        navigate(fullPath);
    }

    return (
        <div className="repodetail-main container">
            <h2>{projectDetails.project_Name}</h2>
            <div className="repodetail-data">
                <div className="repodetail-data-child">
                    <p className="header-values">Client Name : <span className="values">{projectDetails.client_Name}</span></p>
                    <p className="header-values">description : <span className="values">{projectDetails.description}</span> </p>
                    <p className="header-values">Responsible : <span className="values">{projectDetails.employeeName}</span></p>
                </div>
                <div className="repodetail-data-child">
                    <p>Created on :{projectDetails.created_On} </p>
                    <p>Due Date : {projectDetails.dueDate}</p>
                    <p>status : {projectDetails.status} </p>
                </div>
            </div>
            <div className="project-header">
                <h2>Tickets</h2>
                <button onClick={handleNavigateSmooth}>Create Ticket</button>
            </div>
            <div className="repo-body">
                <ul className="ticket-list">
                    {Array.isArray(TicketsByID) && TicketsByID?.map((ticket) => (
                        <li key={ticket.issue_Id}
                            onClick={() => handleRepoDetail(ticket.issue_Id)}
                            className={`ticket ${ticket.status}`}  >
                            {/* <Link to={`${proj.issue_Id}`} className="ticket-link"> */}
                            <div className="ticket-header">
                                <span className="ticket-title">{ticket.issue_Title}</span>
                                <span className={`ticket-status ${ticket.status}`}>{ticket.status}</span>
                            </div>
                            <div className="ticket-details">
                                <span className="ticket-assignee">Assigned to: {ticket.assignee_Name}</span>
                                <span className="ticket-created">Created: {ticket.created_On}</span>
                                {/* <span className="ticket-comments">{ticket.comments} comments</span> */}
                            </div>
                            <div className="ticket-labels">
                                {ticket.labels_JSON.map((label, i) => (
                                    <span key={i} className={`ticket-label ${label}`}
                                        style={{ "--label-color": label.labeL_COLOR }}
                                    >{label.labeL_TITLE}</span>
                                ))}
                            </div>
                            {/* </Link> */}
                        </li>
                    ))}
                </ul>
            </div>
        </div>
    );
};

export default ProjectDetails;