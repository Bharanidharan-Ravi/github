// HtmlDateInput.jsx
const HtmlDateInput = ({ name, label, value, error, onChange }) => (
  <div>
    <label>{label}</label>
    <input
      type="date"
      value={value || ""}
      onChange={(e) => onChange(name, e.target.value)}
    />
    {error && <div style={{ color: "red" }}>{error}</div>}
  </div>
);
export default HtmlDateInput;
