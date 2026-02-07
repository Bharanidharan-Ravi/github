import axios from "axios";
import { useCustomStore } from "../store"

// const API_URL = "https://crm.canarahydraulics.com:8088/api";
const API_URL = import.meta.env.VITE_API_BASE_URL;
const environment = import.meta.env.VITE_ENVIRONMENT;
console.log("api :", API_URL, environment);

const api = axios.create({
    baseURL: API_URL,
    timeout: 100000,
    headers: {
        'Content-Type': 'application/json',
    },
});

api.interceptors.request.use(
    (config) => {
        const { setGlobalLoading } = useCustomStore.getState();
        if (!config._silent) setGlobalLoading(true);
        const userData = sessionStorage.getItem('user');

        const parsedUserData = userData ? JSON.parse(userData) : null;

        if (parsedUserData) {
            config.headers['wg_token'] = parsedUserData;
        }
        // config.headers['wg_token'] = "x8m0nLLDf7Yc7AK7k/RocuFxxeNs5zN9KYZFwfoBZGr2N76UxEkMbjTc8JgI+ACdbCRBtWfUMOfG599LFMJZmbVyv1zS8NOXqM2PM69aOCOJ6/9DAAHtZvGBZqJaH+nLjkkvZnG9Gsf+oYpubRAZWtjsoPuaH94jkHj2f503eHJxnTWWJ4Lf6cCMct3/+8fIjsNbGb/yGIgaBbmSCXdUgKcRBIG0zOcFVs3HvxIjQS4gvAob1A/5/y7w0KPY7Y7ivVS/VGRv+lNafeQsDrnhBy620xICH2RwT6EfJmkpJ8UaigTmbOeU4uJ5F1iK0BRhRcxtYRcl33u1vlHPhtsw/4JvrluKxn0WHtI7/CmhGrrZTZJ1eYpISjUrs3K6GNJRxK/M/TddFFa4fCyzBQNqivs2a1dfIAHvW7rVpECOKfUijQ4qr74AAO1QIax8rzxm7mR2BPCUOFbcSxbxF7p0RztqvetN4px2p2ANjFnEoxIRpx78apCh4ZFqylbFV67gE+DEu73pqVz6E9BiHi/q3B6JjepJpble4NI+Tgte/lZCILgssO1M17wp6fxz8lXJgL9C1++yojQ8xgRMXZ1njIIbrP8R8r0yHJTi9ciZ7Tav9ohtAt9tLl2plFmEDD07"
        return config;
    },
    (error) => {
        useCustomStore.getState().setGlobalLoading(false);
        useCustomStore.getState().setGlobalError("Request initialization faild");
        return Promise.reject(error);
    }
);

api.interceptors.response.use(
    (response) => {

        const { setGlobalLoading, setGlobalSuccess } = useCustomStore.getState();
        setGlobalLoading(false);
        // const { code = 200, message = '', Data = null } = response?.data ?? {};
        // const method = response?.config?.method?.toUpperCase();
        const respData = response?.data ?? {};
        const code = respData.code ?? 200;
        const message = respData.message ?? '';
        const data = respData.Data ?? respData.data ?? null; // fallback to either Data or data

        const method = response?.config?.method?.toUpperCase();
        console.log("response data :", response);

        if (code >= 200 && code < 300) {
            if (message && (method === 'POST' || method === 'PUT' || method === 'DELETE') && message !== 'NO') {
                setGlobalSuccess(message);
            }

            return data;
        }

        return Promise.reject(new Error(message));
    },
    (error) => {
        const { setGlobalLoading, setGlobalError } = useCustomStore.getState();
        setGlobalLoading(false);
        console.log("error :", error);

        const message =
            error?.response?.data?.errorMessage ||
            // error?.message ||
            'Unexpected error occurred.';
        setGlobalError(message)
        store.dispatch(addError(message));
        return Promise.reject(error);
    }
);


export default api;