/**
 * Generic API executor.
 * Acts like a universal thunk.
 *
 * @param {object} apiClient - Axios instance
 */
export function createApiExecutor(apiClient) {
  return async function executeApi(options) {
    const {
      url,
      method = "GET",
      payload,
      params,
      config,
    } = options;

    switch (method) {
      case "GET":
        return apiClient.get(url, { params, ...config });

      case "POST":
        return apiClient.post(url, payload, config);

      case "PUT":
        return apiClient.put(url, payload, config);

      case "DELETE":
        return apiClient.delete(url, { data: payload, ...config });

      case "PATCH":
        return apiClient.patch(url, payload, config);

      default:
        throw new Error(`Unsupported HTTP method: ${method}`);
    }
  };
}
