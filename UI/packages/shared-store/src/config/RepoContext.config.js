export const RepoContextConfig = {
  contextKey: "repo",

  url: {
    prefix: "/r/:repoId"
  },

  extractParams: (segments) => ({
    Repo_Id: segments.repoId
  }),

  defaultRedirect: (repoId) => `/user/r/${repoId}/tickets`
};
