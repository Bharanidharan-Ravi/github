import React, { useState, useMemo } from "react";
import { IoMdTrash, IoMdAdd } from "react-icons/io";
import Modal from "../Modal/Modal";
import Select from "react-select";

const customStyles = {
  content: {
    top: "50%",
    left: "50%",
    right: "auto",
    bottom: "auto",
    marginRight: "-50%",
    transform: "translate(-50%, -50%)",
    width: "600px",
    padding: "24px",
    zIndex: 1050,
    borderRadius: "8px",
    boxShadow: "0 4px 6px -1px rgba(0, 0, 0, 0.1)",
  },
  overlay: { zIndex: 1040, backgroundColor: "rgba(0,0,0,0.5)" },
};

const TicketManager = ({ 
    value = [], 
    onChange, 
    employeeOptions = [], 
    departmentOptions = [], 
    disabled,
    ticketConfig = {} 
}) => {
  const [isModalOpen, setIsModalOpen] = useState(false);

  // Default Config
  const labels = {
      modalTitle: ticketConfig.modalTitle || "Add Feature / Parent Ticket",
      buttonText: ticketConfig.buttonText || "Add Ticket",
      fields: {
          title: { label: "Ticket Title" },
          departments: { label: "Involved Teams", placeholder: "Select Teams (e.g. Dev, QA)..." },
          dueDate: { label: "Due Date" },
          description: { label: "Description" },
          ...ticketConfig.fields 
      },
      headers: {
          title: "Feature",
          teamMap: "Team & Assignees", // 🟢 NEW HEADER
          dueDate: "Due",
          action: " ",
          ...ticketConfig.tableHeaders 
      }
  };

  const [newTicket, setNewTicket] = useState({
    title: "",
    description: "",
    departments: [], 
    // 🟢 Key Change: assignments is now an Object mapping DeptValue -> Array of Users
    // Example: { "Development": [UserA], "QA": [UserB] }
    assignments: {}, 
    dueDate: ""
  });

  // Helper to get employees for a specific department
  const getEmployeesForDept = (deptValue) => {
      return employeeOptions.filter(emp => emp.value.dept === deptValue);
  };

  const handleAddTicket = () => {
    if (!newTicket.title || newTicket.departments.length === 0) {
      alert("Title and at least one Department are required!");
      return;
    }

    const updatedTickets = [...value, { ...newTicket, id: Date.now() }];
    onChange(updatedTickets);
    
    // Reset
    setNewTicket({ title: "", description: "", departments: [], assignments: {}, dueDate: "" });
    setIsModalOpen(false);
  };

  const handleRemoveTicket = (index) => {
    const updatedTickets = value.filter((_, i) => i !== index);
    onChange(updatedTickets);
  };

  // Handle changes in the per-department dropdowns
  const handleAssignmentChange = (deptValue, selectedUsers) => {
      setNewTicket(prev => ({
          ...prev,
          assignments: {
              ...prev.assignments,
              [deptValue]: selectedUsers || []
          }
      }));
  };

  return (
    <div className="ticket-manager-container mt-3">
      <div className="d-flex justify-content-between align-items-center mb-2">
        <h6 className="m-0 fw-bold text-secondary"></h6>
        {!disabled && (
          <button type="button" className="minimal-button small" onClick={() => setIsModalOpen(true)}>
            <IoMdAdd /> {labels.buttonText}
          </button>
        )}
      </div>

      {/* 🟢 TABLE SHOWING THE MAPPING */}
      {value.length > 0 ? (
        <div className="table-responsive">
          <table className="table table-bordered table-hover table-sm minimal-table">
            <thead className="table-light">
              <tr>
                <th style={{width: '25%'}}>{labels.headers.title}</th>
                <th style={{width: '50%'}}>{labels.headers.teamMap}</th>
                <th style={{width: '15%'}}>{labels.headers.dueDate}</th>
                {!disabled && <th style={{width: '10%'}}>{labels.headers.action}</th>}
              </tr>
            </thead>
            <tbody>
              {value.map((ticket, index) => (
                <tr key={ticket.id || index}>
                  <td className="align-middle fw-medium">{ticket.title}</td>
                  <td>
                    {/* 🟢 Render the Mapping: Dept -> Assignees */}
                    <div className="d-flex flex-column gap-1">
                        {ticket.departments.map(dept => {
                            const assignedUsers = ticket.assignments[dept.value] || [];
                            return (
                                <div key={dept.value} className="d-flex align-items-center border-bottom pb-1" style={{fontSize: '0.85rem'}}>
                                    <span className="badge bg-light text-dark border me-2" style={{minWidth: '80px'}}>
                                        {dept.label}
                                    </span>
                                    {assignedUsers.length > 0 ? (
                                        <div className="d-flex flex-wrap gap-1">
                                            {assignedUsers.map(u => (
                                                <span key={u.value.id} className="text-primary bg-primary bg-opacity-10 px-1 rounded">
                                                    {u.label}
                                                </span>
                                            ))}
                                        </div>
                                    ) : (
                                        <span className="text-muted fst-italic ms-1">Pending Assignment...</span>
                                    )}
                                </div>
                            );
                        })}
                    </div>
                  </td>
                  <td className="align-middle">{ticket.dueDate || "-"}</td>
                  {!disabled && (
                    <td className="text-center align-middle">
                      <IoMdTrash className="cursor-pointer text-danger" onClick={() => handleRemoveTicket(index)} />
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <div className="text-center p-3 border border-dashed rounded bg-light text-muted">
          <small>No tickets yet.</small>
        </div>
      )}

      {/* 🟢 Dynamic Modal Labels */}
      <Modal
        title={labels.modalTitle}
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
      >
        {/* <div className="d-flex justify-content-between align-items-center mb-3">
             <h5 className="m-0">{labels.modalTitle}</h5>
             <button className="btn-close" onClick={() => setIsModalOpen(false)}></button>
        </div> */}

      <div className="modal-body d-flex flex-column gap-3">
          {/* Title */}
          <div>
            <label className="form-label small fw-bold">{labels.fields.title.label} <span className="text-danger">*</span></label>
            <input 
                className="form-control" 
                value={newTicket.title} 
                onChange={(e) => setNewTicket({...newTicket, title: e.target.value})} 
            />
          </div>

          {/* 1. Select Departments First */}
          <div>
              <label className="form-label small fw-bold">{labels.fields.departments.label} <span className="text-danger">*</span></label>
              <Select
                  isMulti
                  options={departmentOptions}
                  value={newTicket.departments}
                  onChange={(selected) => {
                      // Keep existing assignments if the dept is still selected, remove if unchecked
                      const newAssignments = { ...newTicket.assignments };
                      // (Optional cleanup logic could go here, but keeping it simple is fine too)
                      setNewTicket({...newTicket, departments: selected || []});
                  }}
                  placeholder={labels.fields.departments.placeholder}
                  menuPortalTarget={document.body}
                  styles={{ menuPortal: base => ({ ...base, zIndex: 9999 }) }}
              />
          </div>

          {/* 2. 🟢 DYNAMIC ASSIGNMENT ROWS */}
          {newTicket.departments.length > 0 && (
              <div className="p-3 bg-light rounded border">
                  <h6 className="small fw-bold text-secondary mb-3">Assign Members per Team</h6>
                  <div className="d-flex flex-column gap-3">
                      {newTicket.departments.map(dept => (
                          <div key={dept.value} className="row align-items-center">
                              <div className="col-4">
                                  <span className="badge bg-white text-dark border px-2 py-1">{dept.label}</span>
                              </div>
                              <div className="col-8">
                                  <Select
                                      isMulti
                                      placeholder={`Who will work on ${dept.label}?`}
                                      options={getEmployeesForDept(dept.value)} // 🟢 Only shows employees in this dept
                                      value={newTicket.assignments[dept.value] || []}
                                      onChange={(selected) => handleAssignmentChange(dept.value, selected)}
                                      menuPortalTarget={document.body}
                                      styles={{ menuPortal: base => ({ ...base, zIndex: 9999 }) }}
                                  />
                              </div>
                          </div>
                      ))}
                  </div>
              </div>
          )}

          {/* Description & Due Date */}
          <div className="row">
              <div className="col-md-8">
                <label className="form-label small fw-bold">{labels.fields.description.label}</label>
                <textarea 
                    className="form-control" 
                    rows={2}
                    value={newTicket.description}
                    onChange={(e) => setNewTicket({...newTicket, description: e.target.value})} 
                />
              </div>
              <div className="col-md-4">
                 <label className="form-label small fw-bold">{labels.fields.dueDate.label}</label>
                 <input 
                    type="date" 
                    className="form-control" 
                    value={newTicket.dueDate} 
                    onChange={(e) => setNewTicket({...newTicket, dueDate: e.target.value})} 
                />
              </div>
          </div>

          <div className="d-flex justify-content-end gap-2 mt-4 pt-3 border-top">
            <button className="btn btn-light" onClick={() => setIsModalOpen(false)}>Cancel</button>
            <button className="btn btn-primary px-4" onClick={handleAddTicket}>{labels.buttonText}</button>
          </div>
        </div>
      </Modal>
    </div>
  );
};

export default TicketManager;
