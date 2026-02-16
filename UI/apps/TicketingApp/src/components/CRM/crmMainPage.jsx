import { FormEngine } from "@formstack/react-input-engine";
import { useOpportunityApiStore } from "./store/useOpportunityApiStore";
import { useEffect } from "react";
import { getOpportunityFields } from "./Config/getOpportunityFields";
import { useState } from "react";
import { useOpportunityFormStore } from "./store/useOpportunityFormStore";
import AttachmentPreview from "./Attachment";
const CRMmainPage = () => {
    const { masters, fetchMasters } = useOpportunityApiStore();
    const { values, setValue, fields, setField, errors } = useOpportunityFormStore();
    // const [fields, setFields] = useState([]); 
    ``
    useEffect(() => {
        fetchMasters();
    }, []);

    useEffect(() => {
        setField(getOpportunityFields(masters, values));
    }, [masters, values]);
console.log("error :", errors);

    return (
        <div>
            <h1>CRM Main Page</h1>

            <FormEngine
                errors={errors}
                fields={fields}
                values={values}
                onChange={setValue}
            />
            <AttachmentPreview />
        </div>
    );
};

export default CRMmainPage;