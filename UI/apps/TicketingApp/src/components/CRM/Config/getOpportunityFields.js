import { getCustomerOptions } from "../Shared/Utlities";

export const getOpportunityFields = (masters, values) => [
    {
        key: "customerCode",
        label: "Customer Code",
        type: "select",
        options: getCustomerOptions(masters, values),
        // options: masters?.customerDetails.map(c => ({
        //     label: c.customerName,
        //     value: c.customerCode
        // })),
        required: true
    },
    {
        key: "customerName",
        label: "Customer Name",
        type: "select",
        options: masters?.contactPersonDetails.map(s => ({
            label: s.contact_Person,
            value: s.contact_Person_Code
        })),
        required: true
    },
    {
        key: "salesEmployee",
        label: "Sales Employee",
        type: "select",
        options: masters?.salesPersonDetails.map(s => ({
            label: s.slpName,
            value: s.slpCode
        }))
    },
    {
        key: "PotentialAmount",
        label: "Potential Amount",
        type: "text",
        required: true
    },
    // {
    //     key: "salesEmployee",
    //     label: "Sales Employee",
    //     type: "select",
    //     options: masters?.salesPersonDetails.map(s => ({
    //         label: s.slpName,
    //         value: s.slpCode
    //     }))
    // },
    {
        key: "followUpStage",
        label: "Follow up Stage",
        type: "select",
        options: masters?.stagesList.map(s => ({
            label: s.descript,
            value: s.stepId
        })),
        required: true
    },
    {
        key: "PredicatedClosingDate",
        label: "Predicted Closing Date",
        type: "date",
        required: true
    },
];

