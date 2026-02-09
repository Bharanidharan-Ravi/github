export const validateValue = (field, value) => {
  const v = field.validation;

  // 🔒 no validation config → skip
  if (!v || !v.type) {
    return { value, error: null };
  }
console.log("Validating", { field, value,v } );

  /* =====================
     NUMBER / DECIMAL
     ===================== */
  if (v.type === "number") {
    if (!/^[\d.]*$/.test(value)) {
      return { value, error: "Only numbers are allowed" };
    }

    if (v.format?.startsWith("decimal")) {
      const [, p, s] =
        v.format.match(/decimal\((\d+),(\d+)\)/) || [];

      const parts = value.split(".");
      if (parts.length > 2) {
        return { value, error: "Invalid number format" };
      }

      if (parts[0].length > Number(p)) {
        return { value, error: `Maximum ${p} digits allowed` };
      }

      if (parts[1]?.length > Number(s)) {
        return {
          value,
          error: `Maximum ${s} decimal places allowed`
        };
      }
    }
  }

  /* =====================
     DATE
     ===================== */
  if (v.type === "date") {
    let date;

    if (v.smartInput && /^[+-]\d+$/.test(value)) {
      date = new Date();
      date.setDate(date.getDate() + Number(value));
    } else {
      date = new Date(value);
    }

    if (isNaN(date)) {
      return { value, error: "Invalid date" };
    }

    if (v.allowPastDate === false) {
      const today = new Date(new Date().toDateString());
      if (date < today) {
        return { value, error: "Past date not allowed" };
      }
    }

    return {
      value: date.toISOString().split("T")[0],
      error: null
    };
  }

  return { value, error: null };
};
