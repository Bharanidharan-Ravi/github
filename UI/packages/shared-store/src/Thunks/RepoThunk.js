//#region  Repo api

import api from "../ApiService/Api";

export const GetAllReponew = async () => {
    // const { setRepoData } = useCustomStore.getState();
    try {
        const response = await api.get('tickets/Repository/GetAllRepoData');
        // setRepoData(response);
        return response;
    } catch (error) {
        return rejectWithValue(error.response?.data?.errorMessage || 'Invalid username or password');
    }
};

// export const postRepo = async (payload) => {
//     // const { setTicketMaster } = useCustomStore.getState();
//     try {
//         const response = await api.post('Repo/PostRepo', payload);
//         // setTicketMaster(response);
//         return response;
//     } catch (error) {
//         return rejectWithValue(error.response?.data?.errorMessage || 'Invalid username or password');
//     }
// };

//#endregion