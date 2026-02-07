import axios from "axios";
import { useCustomStore } from "../store"

const API_URL = "https://crm.canarahydraulics.com:8088/api";
// const API_URL = import.meta.env.VITE_API_BASE_URL;
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

        // if (parsedUserData) {
        //     config.headers['wg_token'] = parsedUserData;
        // }
        config.headers['wg_token'] = "KAD0tUYqL0yTlueNFBR8S/eh1xfFM3TmffjOpHpHYAN92PLRw76ghTcWRNGPGeBduYBaBKk5LZkrYr44OI+lIPvOQ1dM1YOHVgFLKruH/blIDZOfqLlNy2RVrePmVnmOPsZpADNPviDnlJVrSEilFHMB25P5CU5yBuuoZGIPe4ZU9+lfkeoFdYCBGneAPyo3ocvmBjbW2pj+xrc1qNrTX6f/DADqBLx7pWhrvGk3mA37DnFnSt08M28SLCn/aaSFu95uKEj4Uo+oyBHbmnMm9YhJGW3Lkf1Ls8dthqqfbgxHiJureM4jXXnQ4O1+XfI7CQfS/QR9vRjcPQ0gzXoZe6cl1KGG3/YPTmFPpOOzJdT6uhfuByw1pPihjjFAY0iA0cjXaEb+RzznSDz4p9E49pByVq2dKqpbtAnaNMSYceR2s2fKlocPi6C04oeHGndRYzOegCcVH3uCtE/au7tLoMHAa7TkO5lVXha6zjqM4k1F19ESALKeoxdKG0/tgas4sTkYH5z3vh0vMoqGeJAkBdRjB3owULazYCNU3pBe7tcv3BLMKz6vGwTV537K5+y6dq8Pq2G69/TbMJTqCh9QEkKlRMkHs7gI17IUTeOy5lHv0C6XkNhhLb/rmdMrH3LM"
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
        // console.log("response data :", response);

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