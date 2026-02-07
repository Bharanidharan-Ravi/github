// HtmlSelectInput.jsx
const HtmlSelectInput = ({
  name,
  label,
  value,
  error,
  options = [],
  onChange,
}) => (
  <div>
    <label>{label}</label>
    <select
      value={value || ""}
      onChange={(e) => {
        const opt = options.find((o) => o.value === e.target.value);
        if (!opt) return;
        onChange(name, opt.value, {
          label: opt.label,
          raw: opt,
        });
      }}
    >
      <option value="">Select</option>
      {options.map((o) => (
        <option key={o.value} value={o.value}>
          {o.label}
        </option>
      ))}
    </select>
    {error && <div style={{ color: "red" }}>{error}</div>}
  </div>
);
export default HtmlSelectInput;
