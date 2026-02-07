// HtmlTextInput.jsx
const HtmlTextInput = ({ name, label, value, error, onChange }) => (
  <div>
    <label>{label}</label>
    <input
      value={value || ""}
      onChange={(e) => onChange(name, e.target.value)}
    />
    {error && <div style={{ color: "red" }}>{error}</div>}
  </div>
);
export default HtmlTextInput;
