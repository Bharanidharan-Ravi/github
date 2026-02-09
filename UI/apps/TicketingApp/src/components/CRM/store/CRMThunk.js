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

export const PostImages = async (fileArray) => {
    // const { setRepoData } = useCustomStore.getState();
    try {
        
      const formData = new FormData();

      fileArray.forEach((file) =>
        formData.append("files", file)
      );
        const response = await api.post('Opportunity/UploadFilesToTempAsync', formData);
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
