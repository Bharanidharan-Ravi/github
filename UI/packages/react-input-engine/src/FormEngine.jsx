import TextInput from "./components/TextInput";
import SelectInput from "./components/SelectInput";
import DateInput from "./components/DateInput";

const FormEngine = ({
  fields = [],
  values = {},
  errors = {},
  onChange,
  className = "",
}) => {
  const renderInput = (field) => {
    const commonProps = {
      field,
      value: values[field.key],
      error: errors[field.key],
      onChange,
    };

    switch (field.type) {
      case "select":
        return <SelectInput {...commonProps} />;
      case "date":
        return <DateInput {...commonProps} />;
      default:
        return <TextInput {...commonProps} />;
    }
  };

  return (
    <div className={className}>
      {fields.map((field) => (
        <div key={field.key}>
          {renderInput(field)}
        </div>
      ))}
    </div>
  );
};

export default FormEngine;
