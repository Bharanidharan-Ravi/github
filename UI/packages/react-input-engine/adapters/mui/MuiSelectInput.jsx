import { Autocomplete, TextField } from "@mui/material";

const MuiSelectInput = ({
  name,
  label,
  value,
  error,
  options = [],
  onChange,
  clearable = true,
  disabled = false,
  required,
}) => {
  const selected = options.find((o) => o.value === value?.value) ?? null;
//   console.log("selected :", selected, "value :", value, "options :", options );

  return (
    <Autocomplete
      options={options}
      value={selected}
      isOptionEqualToValue={(o, v) => o.value === v.value}
      disableClearable={!clearable}
      disabled={disabled}
      onChange={(_, option, reason) => {
        // ✅ CLEAR CASE
        if (option === null) {
          onChange(name, "", {
            cleared: true,
          });
          return;
        }

        // ✅ SELECT CASE
        onChange(name, option.value, {
          label: option.label,
          raw: option,
        });
      }}
      clearOnEscape
      renderInput={(params) => (
        <TextField
          {...params}
          label={label}
          error={!!error}
          helperText={error}
          required={required}
        />
      )}
    />
  );
};

export default MuiSelectInput;
