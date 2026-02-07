import api from "../ApiService/Api";
import { useCustomStore } from "../store";

export const GetClientMaster = async () => {
    const { setClientMaster } = useCustomStore.getState();
    try {
        const response = await api.get('tickets/MasterData/GetClients');
        setClientMaster(response);
        return response;
    } catch (error) {
        return rejectWithValue(error.response?.data?.errorMessage || 'Invalid username or password');
    }
};


export const GetEmployees = async () => {
    const { setEmployess } = useCustomStore.getState();
    try {
        const response = await api.get('Login/GetEmployeeMaster');
        setEmployess(response);
        return response;
    } catch (error) {
        return rejectWithValue(error.response?.data?.errorMessage || 'Invalid username or password');
    }
};

export const GetLabelMaster = async () => {
    const { setLabelMaster } = useCustomStore.getState();
    try {
        const response = await api.get('tickets/MasterData/GetLabels');
        setLabelMaster(response);
        return response;
    } catch (error) {
        return rejectWithValue(error.response?.data?.errorMessage || 'Invalid username or password');
    }
};