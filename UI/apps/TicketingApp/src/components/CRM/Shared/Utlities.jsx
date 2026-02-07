export const getCustomerOptions = (masters, values) => {
    if (!values?.salesPerson) {
        return masters.customerDetails.map(c => ({
            label: `${c.customerCode} - ${c.customerName}`,
            value: c.customerCode
        }));
    }

    return masters.customerDetails
        .filter(c => c.salesPersonCode === values.salesPerson)
        .map(c => ({
            label: `${c.customerCode} - ${c.customerName}`,
            value: c.customerCode
        }));
};
