import React from "react";
import { useNavigate } from "react-router-dom";
import { useTicketsContext } from "../Context/TicketsContext";

const DashboardView = ({ isModal }) => {
    const navigate = useNavigate();
    const {  } = useTicketsContext();
  
    const closeModal = () => {
      navigate(-1); // go back to previous route
    };
  
    return (
      <div className={`modal-overlay ${isModal ? "show" : ""}`}>
        <div className="modal-content">
          <h2>Create Ticket</h2>
          <form>
            <input placeholder="Title" />
            <textarea placeholder="Description"></textarea>
            <div className="modal-actions">
              <button type="button" onClick={closeModal}>
                Cancel
              </button>
              <button type="submit">Save</button>
            </div>
          </form>
        </div>
      </div>
    );
  };
  
  export default DashboardView;