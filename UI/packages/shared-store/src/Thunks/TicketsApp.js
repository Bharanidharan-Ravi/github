import api from "../ApiService/Api";
import { useCustomStore } from "../store";

//#region  Ticket api
export const GetAllIssues = async () => {
    const { setTicketMaster } = useCustomStore.getState();
    try {
        const response = await api.get('tickets/TicketingContoller/GetAllIssueData');
        setTicketMaster(response);
        return response;
    } catch (error) {
        return rejectWithValue(error.response?.data?.errorMessage || 'Invalid username or password');
    }
};

export const PostIssues = async (payload) => {
    // const { setTicketMaster } = useCustomStore.getState();
    try {
        const response = await api.post('tickets/TicketingContoller/CreateIssue', payload);
        // setTicketMaster(response);
        return response;
    } catch (error) {
        return rejectWithValue(error.response?.data?.errorMessage || 'Invalid username or password');
    }
};

export const postImage = async (file)=> {
    const { setReturnTemp } = useCustomStore.getState();
    console.log("filet :", file);
    const formData = new FormData();
    formData.append("files", file);
    const config = {
        headers: {
          'Content-Type': 'multipart/form-data',
        },
      };
    try {
        const response = await api.post('tickets/TicketingContoller/UploadFilesToTempAsync', 
            formData, 
            config
          );
        setReturnTemp(response);
        console.log("response :", response);
        
        return response;
    } catch (error) {
        return rejectWithValue(error.response?.data?.errorMessage || 'Invalid username or password');
    }
}

export const RemoveImage = async (payload) => {
    // const { setTicketMaster } = useCustomStore.getState();
    try {
        const response = await api.post('tickets/TicketingContoller/CleanupTempFiles', payload);
        // setTicketMaster(response);
        return response;
    } catch (error) {
        return rejectWithValue(error.response?.data?.errorMessage || 'Invalid username or password');
    }
};

//#endregion

//#region  Repo api

export const GetAllRepo = async () => {
    const { setRepoData } = useCustomStore.getState();
    try {
        const response = await api.get('tickets/Repository/GetAllRepoData');
        setRepoData(response);
        return response;
    } catch (error) {
        return rejectWithValue(error.response?.data?.errorMessage || 'Invalid username or password');
    }
};

export const postRepo = async (payload) => {
    // const { setTicketMaster } = useCustomStore.getState();
    try {
        const response = await api.post('Repo/PostRepo', payload);
        // setTicketMaster(response);
        return response;
    } catch (error) {
        return rejectWithValue(error.response?.data?.errorMessage || 'Invalid username or password');
    }
};

//#endregion

//#region Project API
export const getProject = async ({ clientId, repoId }) => {
    const { setProject } = useCustomStore.getState();
    try {
        let queryString = 'tickets/Project/GetProjMaster';

        if (clientId) {
            queryString += `?clientId=${clientId}&`;
        }

        if (repoId) {
            queryString += `?repoId=${repoId}&`;
        }

        // Remove the trailing "&" if there is one
        if (queryString.endsWith('&')) {
            queryString = queryString.slice(0, -1);
        }

        const response = await api.get(queryString);
        setProject(response)
        // setTicketMaster(response);
        return response;
    } catch (error) {
        return rejectWithValue(error.response?.data?.errorMessage || 'Invalid username or password');
    }
};

export const PostProject = async (payload) => {
    // const { setTicketMaster } = useCustomStore.getState();
    try {
        const response = await api.post('tickets/Project/PostProject', payload);
        // setTicketMaster(response);
        return response;
    } catch (error) {
        return rejectWithValue(error.response?.data?.errorMessage || 'Invalid username or password');
    }
};

export const GetTicketById = async ({ ProjectId, issueId }) => {
    const { setProjTickets } = useCustomStore.getState();
    try {
        let queryString = 'tickets/TicketingContoller/GetIssuesbyId';

        if (ProjectId) {
            queryString += `?ProjectId=${ProjectId}&`;
        }

        if (issueId) {
            queryString += `?issueId=${issueId}&`;
        }

        // Remove the trailing "&" if there is one
        if (queryString.endsWith('&')) {
            queryString = queryString.slice(0, -1);
        }

        const response = await api.get(queryString);
        // const response = await api.get('tickets/TicketingContoller/GetIssuesbyId?ProjectId=');
        setProjTickets(response);
        return response;
    } catch (error) {
        return rejectWithValue(error.response?.data?.errorMessage || 'Invalid username or password');
    }
};

//#endregion

//#region Thread data 

export const PostThread = async (payload) => {
    // const { setTicketMaster } = useCustomStore.getState();
    try {
        const response = await api.post('tickets/TicketingContoller/CreateThreads', payload);
        // setTicketMaster(response);
        return response;
    } catch (error) {
        return rejectWithValue(error.response?.data?.errorMessage || 'Invalid username or password');
    }
};

export const GetThreadList = async ({ ticketId }) => {
    const { setThreadList } = useCustomStore.getState();
    try {
        let queryString = `tickets/ViewTicketing/GetThreadData?IssuesId=${ticketId}`;

        const response = await api.get(queryString);
        setThreadList(response)
        // setTicketMaster(response);
        return response;
    } catch (error) {
        return rejectWithValue(error.response?.data?.errorMessage || 'Invalid username or password');
    }
};

//#endregion