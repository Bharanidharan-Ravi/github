// MuiDateInput.jsx
import { TextField } from "@mui/material";

const MuiDateInput = ({ name, label, value, error, onChange,required }) => (
  <TextField
    type="date"
    label={label}
    InputLabelProps={{ shrink: true }}
    value={value || ""}
    error={!!error}
    helperText={error}
    required={required}
    onChange={(e) => onChange(name, e.target.value)}
  />
);

export default MuiDateInput;
