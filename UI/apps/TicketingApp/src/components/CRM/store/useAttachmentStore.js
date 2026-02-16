import { create } from "zustand";
// import apiClient from "@/api/apiClient";

export const useAttachmentStore = create((set, get) => ({

  // ============================
  // STATE
  // ============================
  attachments: [],
  loading: false,
  error: null,
  progress: 0,

  // ============================
  // CONFIG (Dynamic per app)
  // ============================
  maxFiles: 5,
  maxSize: 5 * 1024 * 1024, // 5 MB
  allowedTypes: [
    "image/jpeg",
    "image/png",
    "application/pdf",
    "application/vnd.ms-excel",
    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
  ],

  // ============================
  // VALIDATE FILES
  // ============================
  validateFiles: (files) => {
    const { maxFiles, maxSize, allowedTypes, attachments } = get();

    // Max file count
    if (attachments.length + files.length > maxFiles) {
      throw new Error(
        `Max ${maxFiles} files allowed`
      );
    }

    for (let file of files) {

      // Type validation
      if (!allowedTypes.includes(file.type)) {
        throw new Error(
          `File type ${file.type} not allowed`
        );
      }

      // Size validation
      if (file.size > maxSize) {
        throw new Error(
          `${file.name} exceeds max size`
        );
      }
    }
  },

  // ============================
  // UPLOAD FILES
  // ============================
  uploadFiles: async (files) => {
    try {
      const fileArray = Array.from(files);

      // Validate first
      get().validateFiles(fileArray);

      set({
        loading: true,
        error: null,
        progress: 0,
      });

      const formData = new FormData();

      fileArray.forEach((file) =>
        formData.append("files", file)
      );

    //   const res = await apiClient.post(
    //     "/attachment/upload",
    //     formData,
    //     {
    //       headers: {
    //         "Content-Type":
    //           "multipart/form-data",
    //       },

    //       // Progress tracking
    //       onUploadProgress: (event) => {
    //         const percent = Math.round(
    //           (event.loaded * 100) /
    //           event.total
    //         );

    //         set({ progress: percent });
    //       },
    //     }
    //   );

      // Map response + preview
      const uploaded = res.data.map(
        (file, index) => ({
          fileName: file.fileName,
          tempPath: file.fullPath,
          previewUrl:
            URL.createObjectURL(
              fileArray[index]
            ),
          size: fileArray[index].size,
          type: fileArray[index].type,
        })
      );

      // Append to store
      set((state) => ({
        attachments: [
          ...state.attachments,
          ...uploaded,
        ],
        loading: false,
        progress: 100,
      }));

    } catch (err) {
      set({
        error: err.message,
        loading: false,
        progress: 0,
      });
    }
  },

  // ============================
  // REMOVE FILE
  // ============================
  removeFile: async (index) => {
    const file =
      get().attachments[index];

    if (!file) return;

    try {
      await apiClient.post(
        "/attachment/delete-temp",
        {
          filePath: file.tempPath,
        }
      );

      set((state) => ({
        attachments:
          state.attachments.filter(
            (_, i) => i !== index
          ),
      }));

    } catch (err) {
      console.error(err);
    }
  },

  // ============================
  // CLEAR ALL
  // ============================
  clearAll: () => {
    set({
      attachments: [],
      progress: 0,
    });
  },

  // ============================
  // DYNAMIC CONFIG SETTER
  // ============================
  setConfig: (config) => {
    set(config);
  },

}));
