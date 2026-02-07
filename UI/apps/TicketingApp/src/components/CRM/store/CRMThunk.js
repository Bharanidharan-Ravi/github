import api from "../../../../../../packages/shared-store/src/ApiService/Api";

export const GetAllReponew = async () => {
    // const { setRepoData } = useCustomStore.getState();
    try {
        const response = await api.get('Opportunity/OpprFieldsData');
        // setRepoData(response);
        return response;
    } catch (error) {
        return rejectWithValue(error.response?.data?.errorMessage || 'Invalid username or password');
    }
};

export const GetAllCustomer = async () => {
    // const { setRepoData } = useCustomStore.getState();
    try {
        const response = await api.get('Customer/CustomerMaster?filter=0');
        // setRepoData(response);
        return response;
    } catch (error) {
        return rejectWithValue(error.response?.data?.errorMessage || 'Invalid username or password');
    }
};
