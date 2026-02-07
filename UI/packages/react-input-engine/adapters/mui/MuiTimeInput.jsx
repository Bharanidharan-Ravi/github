// MuiTimeInput.jsx
import { TextField } from "@mui/material";

const MuiTimeInput = ({ name, label, value, error, onChange }) => (
  <TextField
    type="time"
    label={label}
    InputLabelProps={{ shrink: true }}
    value={value || ""}
    error={!!error}
    helperText={error}
    onChange={(e) => onChange(name, e.target.value)}
  />
);

export default MuiTimeInput;
