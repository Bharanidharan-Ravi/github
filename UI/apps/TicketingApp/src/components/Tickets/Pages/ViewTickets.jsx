import { useEffect, useRef, useState } from "react";
import "../ViewTickets.css";
import { useTicketStore } from "../TicketStore/TicketStore.js";
import { useCustomStore, GetAllIssues, GetThreadList } from "shared-store";
import { removeEventHandler, addEventHandler } from "shared-signalr";
import { Link, useNavigate } from "react-router-dom";
import SearchDiv from "../../../Shared/searchDiv/searchDiv.jsx";

const ViewTickets = () => {
    const { setTickets, fetchFilterTickets, searchTerm, tickets, setThreadList, setThreadIssuesData } = useTicketStore();
    const { TicketMaster } = useCustomStore();
    const ThreadList = useCustomStore(state => state.ThreadList);
    const navigate = useNavigate();
    const [detailTrigger, setDetsilTrigger] = useState(false);
    const [ticketId, setTicketId] = useState(null);

    const fetchData = async () => {
        if (TicketMaster && TicketMaster.length > 0) {
            setTickets(TicketMaster);
        } else {
            const result = await GetAllIssues();
            setTickets(result);
        }
    };
    console.log("tickets :", tickets, ThreadList);

    useEffect(() => {
        if (detailTrigger && ThreadList && ticketId) {
            setThreadList(ThreadList.threadData);
            setThreadIssuesData(ThreadList.issuesData)
            navigate(ticketId)
        }
    }, [ThreadList, detailTrigger, ticketId])

    useEffect(() => {
        // const onClientCreated = (ticket) => {
        //     console.log("Ticket Created: ", ticket);
        // }
        // const onEmployeeCreated = (ticket) => {
        //     console.log("Ticket Created: ", ticket);
        // }
        // const onUpdated = (ticket) => {
        //     console.log("Ticket Created: ", ticket);
        // }
        // const onDeleted = (ticket) => {
        //     console.log("Ticket Created: ", ticket);
        // }

        // addEventHandler("ClientTicketCreated", onClientCreated);
        // addEventHandler("EmployeeTicketCreated", onEmployeeCreated);
        // addEventHandler("TicketUpdated", onUpdated);
        // addEventHandler("TicketDeleted", onDeleted);
        // fetchData();
        // return () => {
        //     removeEvenHandler("ClientTicketCreated", onClientCreated);
        //     removeEvenHandler("EmployeeTicketCreated", onEmployeeCreated);
        //     removeEvenHandler("TicketUpdated", onUpdated);
        //     removeEvenHandler("TicketDeleted", onDeleted);
        // }
    }, []);

    const openModal = () => {
        // setShowForm(true);
        navigate('create', { replace: true });
    }
    const handleTicketClick = async(ticketId) => {
        if (ticketId) {
            console.log("ticketId:",ticketId);
            
            const result = await GetThreadList({ticketId});
            console.log("result :", result);
            setTicketId(ticketId);
            setDetsilTrigger(true);
        }
    }
    return (
        <div className="tickets-container container">
            <div className="repo-header">
                <SearchDiv
                    searchValue={searchTerm}
                    onChange={fetchFilterTickets}
                    openModal={openModal}
                    placeholder={"Tickets"}
                />
            </div>

            <ul className="ticket-list">
                {tickets && tickets?.length > 0 ? (
                    tickets.map((ticket) => (
                        <li key={ticket.issue_Id} className={`ticket ${ticket.status}`} onClick={() => handleTicketClick(ticket.issue_Id)}>
                            {/* <Link to={`${ticket.issue_Id}`} className="ticket-link"> */}
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
                                    style={{"--label-color": label.labeL_COLOR}}
                                    >{label.labeL_TITLE}</span>
                                ))}
                            </div>
                            {/* </Link> */}
                        </li>
                    ))
                ) : (
                    <p className="no-results">No tickets found.</p>
                )}
            </ul>
        </div>
    );
};

export default ViewTickets;