import { FormEngine } from "@formstack/react-input-engine";
import { useOpportunityApiStore } from "./store/useOpportunityApiStore";
import { useEffect } from "react";
import { getOpportunityFields } from "./Config/getOpportunityFields";
import { useState } from "react";
import { useOpportunityFormStore } from "./store/useOpportunityFormStore";

const CRMmainPage = () => {
    const { masters, fetchMasters } = useOpportunityApiStore();
    const { values, errors, setValue } = useOpportunityFormStore();
    const [fields, setFields] = useState([]);
    useEffect(() => {
        fetchMasters();
    }, []);
    // console.log("masters :", masters);
    useEffect(() => {
        // console.log("Updated masters :", masters);
        setFields(getOpportunityFields(masters));
    }, [masters]);


    return (
        <div>
            <h1>CRM Main Page</h1>
            <FormEngine
                fields={fields}
                onChange={setValue}
                values={values}
            />
        </div>
    );
}

export default CRMmainPage;