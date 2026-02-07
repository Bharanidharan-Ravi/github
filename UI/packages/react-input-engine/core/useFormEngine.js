// core/useFormEngine.js
import { applyValueMapping } from "./valueMapper";

export const useFormEngine = ({ fields, values, setValues }) => {

  const handleChange = (name, value) => {
    setValues(prev => ({ ...prev, [name]: value }));
  };

  const handleSelect = (field, option) => {
    setValues(prev => {
      let next = { ...prev, [field.name]: option.value };

      if (field.mapOnSelect) {
        next = {
          ...next,
          ...applyValueMapping(option, field.mapOnSelect)
        };
      }

      return next;
    });
  };

  return {
    handleChange,
    handleSelect
  };
};
