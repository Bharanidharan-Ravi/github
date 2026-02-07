import axios from "axios";

/**
 * Creates a reusable Axios API client.
 *
 * @param {Object} options
 * @param {string} options.baseURL
 * @param {Function=} options.getToken
 * @param {Function=} options.onRequestStart
 * @param {Function=} options.onRequestEnd
 * @param {Function=} options.onError
 */
export function createApiClient(options) {
  const api = axios.create({
    baseURL: options.baseURL,
    timeout: 100000,
    headers: { "Content-Type": "application/json" },
  });

  api.interceptors.request.use(
    (config) => {
      options.onRequestStart && options.onRequestStart();

      const token = options.getToken && options.getToken();
      if (token) {
        config.headers = config.headers || {};
        config.headers["Authorization"] = token;
      }

      return config;
    },
    (error) => {
      options.onError && options.onError(error);
      return Promise.reject(error);
    }
  );

  api.interceptors.response.use(
    (response) => {
      options.onRequestEnd && options.onRequestEnd();
      return response?.data?.Data ?? response?.data ?? response;
    },
    (error) => {
      options.onRequestEnd && options.onRequestEnd();
      options.onError && options.onError(error);
      return Promise.reject(error);
    }
  );

  return api;
}
