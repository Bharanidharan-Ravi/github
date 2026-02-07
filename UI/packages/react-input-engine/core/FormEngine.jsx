// core/FormEngine.jsx
import { inputRegistry } from "../registry/inputRegistry";

const FormEngine = ({ fields, values, errors = {}, onChange }) => {
    // console.log("FormEngine props:", { fields, values, errors });
    
  return (
    <>
      {fields.map((field) => {
        const Component = inputRegistry?.[field.ui || "html"]?.[field.type];

        if (!Component) return null;

        return (
          <Component
            key={field.name}
            name={field.name}
            label={field.label}
            value={values[field.name]}
            error={errors[field.name]}
            options={field.options}
            onChange={onChange}
            required={field.required} 
            clearable={field.clearable}
            disabled={field.disabled}
          />
        );
      })}
    </>
  );
};

export default FormEngine;

// const FormEngine = ({ fields, values, setValues, masters }) => {

//   const setFieldValue = (name, data) => {
//     setValues(prev => ({ ...prev, [name]: data }));
//   };

//   const resolveOptions = (field) => {
//     if (field.resolveOptions) {
//       return field.resolveOptions({ masters, values });
//     }
//     return field.options || [];
//   };

//   // 🔥 handle dependency reset
//   useEffect(() => {
//     fields.forEach(field => {
//       if (!field.dependsOn) return;

//       field.dependsOn.forEach(dep => {
//         if (!values[dep]) {
//           setFieldValue(field.name, null);
//         }
//       });
//     });
//   }, [values]);

//   return (
//     <>
//       {fields.map(field => {
//         const Component =
//           inputRegistry?.[field.ui || "html"]?.[field.type];

//         if (!Component) {
//           console.error("Missing component", field);
//           return null;
//         }

//         return (
//           <Component
//             key={field.name}
//             field={field}
//             value={values[field.name]}
//             options={resolveOptions(field)}
//             onChange={setFieldValue}
//           />
//         );
//       })}
//     </>
//   );
// };

// export default FormEngine;

////////////////////////////////////////////////////////////////

// import { inputRegistry } from "../registry/inputRegistry";
// import { useFormEngine } from "./useFormEngine";

// const FormEngine = ({ fields, values, setValues }) => {
//   const engine = useFormEngine({ fields, values, setValues });

//   return (
//     <>
//       {fields.map((field) => {
//         const Component = inputRegistry[field.ui || "html"][field.type];
//         if (!Component) {
//           console.error("❌ Missing input component", {
//             fieldName: field.name,
//             ui,
//             type,
//             registry: inputRegistry,
//           });
//           return null;
//         }

//         return (
//           <Component
//             key={field.name}
//             field={field}
//             value={values[field.name]}
//             values={values}
//             onChange={engine.handleChange}
//             onSelect={engine.handleSelect}
//           />
//         );
//       })}
//     </>
//   );
// };

// export default FormEngine;
