const TextInput = ({ field, value, error, onChange }) => {
  return (
    <div className="fe-field">
      <input
        placeholder={field.label}
        value={value || ""}
        onChange={(e) => onChange(field.key, e.target.value)}
      />
      {error && <small className="fe-error">{error}</small>}
    </div>
  );
};

export default TextInput;
