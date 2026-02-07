// MuiTextInput.jsx
import { TextField } from "@mui/material";

const MuiTextInput = ({ name, label, value, error, onChange,required }) => (
  <TextField
    fullWidth
    label={label}
    value={value || ""}
    error={!!error}
    required={required}
    helperText={error}
    onChange={(e) => onChange(name, e.target.value)}
  />
);

export default MuiTextInput;
