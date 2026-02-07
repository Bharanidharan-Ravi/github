import React from "react";
import Select from "react-select";
import "./DynamicForm.css";
import { IoMdTrash } from "react-icons/io";
import DatePicker from "react-datepicker";
import "react-datepicker/dist/react-datepicker.css";
import CommentBar from "../../components/Tickets/Components/CommentBar/AdvancedEditor";
import AdvancedEditor from "../../components/Tickets/Components/CommentBar/AdvancedEditor";
import TicketManager from "../TicketManager/TicketManager";

const DynamicForm = ({
  labels,
  formData,
  onChange,
  onSubmit,
  disabled,
  editMode,
  submitDisabled,
  formErrors,
  useInputField = false,
  showCommentBar = false,
  clientMaster = {},
  onClose,
}) => {
  const handleSubmit = (e) => {
    e.preventDefault();
    if (onSubmit) {
      onSubmit(formData);
    }
  };
  const handleClose = (e) => {
    e.preventDefault();
    onClose();
  };

  useEffect(() => {
    const func = async (...arg) => {
      const res = a + b;
      console.log(res);
    };
    func(a, b);
    
    function funcs() {
      const res = a + b;
      console.log(res);
    }
    funcs(a, b);
  }, []);
  return (
    <form className="container minimal-form p-4" onSubmit={handleSubmit}>
      <div className="row g-3">
        {labels.map((field, i) => {
          const fieldValue = !useInputField
            ? formData[field.ApiValue]
            : formData[field.label];
          // const isDisabled = editMode ? field.editDisable : field.disable;
          const isDisabled =
            disabled || (editMode ? field.editDisable : field.disable);
          const fields = field.isMulti
            ? (fieldValue || []).map((val) => ({
                label: val.name || val.label,
                value: val,
              }))
            : fieldValue
            ? {
                label: fieldValue.field || fieldValue.label,
                value: fieldValue,
              }
            : null;
          const HandlefieldValue = !useInputField ? field.ApiValue : field;
          let inputElement;
          if (field.isMulti && field.type === "text") {
            const values =
              Array.isArray(fieldValue) && fieldValue.length > 0
                ? fieldValue
                : [""];
            inputElement = (
              <>
                {values.map((val, idx) => (
                  <div
                    key={idx}
                    style={{
                      display: "flex",
                      alignItems: "center",
                      marginBottom: "8px",
                    }}
                  >
                    <input
                      type="text"
                      className="minimal-input"
                      placeholder={`${field.label} ${idx + 1}`}
                      value={val}
                      onChange={(e) => {
                        const updated = [...values];
                        updated[idx] = e.target.value;
                        onChange(HandlefieldValue, updated);
                      }}
                      disabled={disabled}
                      autoComplete="off"
                    />
                    {!disabled && idx > 0 && (
                      <button
                        type="button"
                        onClick={() => {
                          const updated = [...values];
                          updated.splice(idx, 1);
                          onChange(HandlefieldValue, updated);
                        }}
                        style={{
                          marginLeft: "5px",
                          background: "none",
                          border: "none",
                          cursor: "pointer",
                        }}
                        title="Remove"
                      >
                        <IoMdTrash color="#dc3545" />
                      </button>
                    )}
                  </div>
                ))}
                {!disabled && (
                  <button
                    className="Add-Achievements"
                    type="button"
                    onClick={() => {
                      const updated = [...values, ""];
                      // onChange(field.label, updated);
                      onChange(HandlefieldValue, updated);
                    }}
                  >
                    + Add {field.label}
                  </button>
                )}
              </>
            );
          }

          // Select field (single or multi)
          else if (field.type === "select") {
            inputElement = (
              <Select
                inputId={`input-${i}`}
                classNamePrefix="minimal-select"
                isDisabled={isDisabled}
                options={field.options}
                value={
                  field.isMulti
                    ? (fieldValue || []).map((val) => ({
                        label: val.name || val.label,
                        value: val,
                      }))
                    : fieldValue
                    ? {
                        label: fieldValue.name || fieldValue.label,
                        value: fieldValue,
                      }
                    : null
                }
                // value={
                //   field.isMulti
                //     ? field.options.filter((opt) =>
                //         (fieldValue || []).includes(opt.value)
                //       )
                //     : field.options.find((opt) => opt.value === fieldValue)
                // }
                onChange={(selectedOption) =>
                  onChange(
                    // field.label,
                    HandlefieldValue,
                    field.isMulti
                      ? selectedOption
                        ? selectedOption.map((opt) => opt.value)
                        : []
                      : selectedOption?.value || ""
                  )
                }
                //  options={field.options.map((opt) => ({ label: opt, value: opt }))}
                placeholder={`Select ${field.label}`}
                isClearable={!field.isMulti}
                isMulti={field.isMulti}
              />
            );
          }

          // Textarea input
          else if (field.type === "textarea") {
            inputElement = (
              <textarea
                id={`input-${i}`}
                className="textarea-input"
                placeholder={`Enter ${field.label}`}
                value={fieldValue || ""}
                onChange={(e) => onChange(HandlefieldValue, e.target.value)}
                disabled={isDisabled}
                rows={4}
              />
            );
          }

          // 🟢 Toggle Button Field
          else if (field.type === "button" && field.toggle) {
            const current = fieldValue ?? field.defaultValue ?? false;

            inputElement = (
              <button
                type="button"
                className={`toggle-btn ${current ? "active" : ""}`}
                onClick={() => onChange(field.name, !current)} // <- important: use field.name, not ApiValue
                style={{
                  padding: "8px 16px",
                  borderRadius: "6px",
                  border: "1px solid #ccc",
                  background: current ? "#4CAF50" : "#eee",
                  color: current ? "white" : "black",
                  cursor: "pointer",
                  width: "100%",
                }}
              >
                {field.label}: {current ? "ON" : "OFF"}
              </button>
            );
          } else if (field.type === "date") {
            inputElement = (
              <DatePicker
                selected={fieldValue ? new Date(fieldValue) : null}
                onChange={(date) => onChange(HandlefieldValue, date)}
                dateFormat="yyyy-MM-dd"
                className="minimal-input"
                placeholderText={`Select ${field.label}`}
                disabled={isDisabled}
              />
            );
          } else if (field.type === "CommentBar") {
            inputElement = (
              <div style={{ marginTop: "15px" }}>
                {/* <CommentBar /> */}
                <AdvancedEditor
                  onChange={onChange}
                  initialContent={formData.description || ""}
                />
              </div>
            );
          } else if (field.type === "ticket-manager") {
            inputElement = (
              <TicketManager
                value={fieldValue || []}
                onChange={(updatedTickets) =>
                  onChange(HandlefieldValue, updatedTickets)
                }
                disabled={isDisabled}
                employeeOptions={field.employeeOptions}
                departmentOptions={field.departmentOptions}
                // 🟢 Pass the config object!
                ticketConfig={field.ticketConfig}
              />
            );
          }
          // Regular input field
          else {
            inputElement = (
              <input
                type={field.type || "text"}
                id={`input-${i}`}
                className="minimal-input"
                placeholder={`Enter ${field.label}`}
                value={fieldValue || ""}
                onChange={(e) => onChange(HandlefieldValue, e.target.value)}
                disabled={isDisabled}
                autoComplete="off"
              />
            );
          }
          const fieldv = !useInputField ? field.label : field.field;
          return (
            <div
              key={i}
              className={`col-6 ${
                field.type === "textarea" || field.type === "CommentBar"
                  ? "col-12"
                  : "col-md-6 col-lg-6"
              }`}
            >
              <label className="minimal-label" htmlFor={`input-${i}`}>
                {field.label}{" "}
                {field.required && <span className="required-asterisk">*</span>}
              </label>
              {inputElement}
              {formErrors[fieldv] && (
                <div className="form-error-text">{formErrors[fieldv]}</div>
              )}
            </div>
          );
        })}
      </div>
      {/* ✅ Show Comment Bar BELOW fields */}
      {showCommentBar && (
        <div style={{ marginTop: "15px" }}>
          {/* <CommentBar /> */}
          <AdvancedEditor
            onChange={onChange}
            initialContent={formData.description || ""}
          />
        </div>
      )}
      {onSubmit && (
        <div className="d-flex justify-content-end mt-5">
          <button
            type="submit"
            className="minimal-button"
            disabled={submitDisabled}
          >
            Submit
          </button>
          <button onClick={handleClose}>Close</button>
        </div>
      )}
    </form>
  );
};

export default DynamicForm;
