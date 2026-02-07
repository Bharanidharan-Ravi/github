import { useCustomStore } from "shared-store";

const TicketModal = () => {
  const labelMaster = useCustomStore(state => state.labelMaster);
  const Project = useCustomStore(state => state.masterProject);
  const Employess= useCustomStore(state => state.Employess);
  
  const ticketFields = [
    { label: "Title", name: "title", type: "text", required: true, ApiValue: "title" },
    { label: "Issue Code", name: "issue_Code", type: "text", required: true, ApiValue: "issue_Code" },
    {
      label: "Label", name: "label", type: "select", required: true, isMulti: true, options: labelMaster?.map(langs => ({
        label: langs.title,
        value: { id: langs.id, name: langs.title },
      })), ApiValue: "label"
    },
    {
      label: "Project", name: "project", type: "select", required: true, isMulti: false, options: Project?.map(langs => ({
        label: langs.project_Name,
        value: { id: langs.id, name: langs.project_Name },
      })), ApiValue: "project"
    },
    { label: "Assigned-to", name: "assignedTo", type: "select", required: true,
      options: Employess?.map(langs => ({
      label: langs.UserName,
      value: { id: langs.UserID, name: langs.UserName },
    })), ApiValue: "assignedTo" },
    { label: "Due Date", name: "dueDate", type: "date", required: true, ApiValue: "dueDate" },
    { label: "Reference Ticket", name: "referenceTicket", type: "select", required: true, ApiValue: "ReferenceTicket" },
    { label: "Description", name: "description", type: "CommentBar", required: true, ApiValue: "description" },
  ];
  return { ticketFields };
};

export default TicketModal;