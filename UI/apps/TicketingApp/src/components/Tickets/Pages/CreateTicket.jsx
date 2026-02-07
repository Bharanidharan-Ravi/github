import React, { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import DynamicForm from "../../../Shared/DynamicForm/DynamicForm.jsx";
import { useTicketStore } from "../TicketStore/TicketStore.js";
import {
  useCustomStore,
  GetLabelMaster,
  getProject,
  GetEmployees,
  PostIssues,
} from "shared-store";
import TicketModal from "../TicketModal/TicketModal.js";
import FormBuilder from "../../../Shared/Formbuilder/FormBuilder.jsx";

const CreateTicket = () => {
  const navigate = useNavigate();
  const { ticketFields } = TicketModal();
  const {
    formData,
    formErrors,
    handleInputChange,
    setProject,
    projectMaster,
    AttachImages,
    resetForm,
  } = useTicketStore();
  const labelMaster = useCustomStore((state) => state.labelMaster);
  const Project = useCustomStore((state) => state.masterProject);
  const Employess = useCustomStore((state) => state.Employess);
  const [tab, setTab] = useState(0);
  const [commentContent, setCommentContent] = useState({
    text: "",
    images: [],
  });
  const [resetKey, setResetKey] = useState(0);

  useEffect(() => {
    const fetchData = async () => {
      if (!labelMaster?.length) {
        const result = await GetLabelMaster();
      }
      if (!Project?.length) {
        const result = await getProject({});
        setProject(result);
      }
      if (!Employess?.length) {
        const result = await GetEmployees();
      }
    };
    if (
      !labelMaster ||
      labelMaster.length === 0 ||
      !Project ||
      Project.length === 0 ||
      !Employess ||
      Employess.length === 0
    ) {
      fetchData();
    }
  }, []);

  const handleCloseModal = () => {
    navigate(-1, { replace: true });
  };

  const onAddComment = async (ticketId, commentText) => {
    const newComment = {
      id: Date.now(),
      author: "Current User",
      text: commentText,
      parentId: null,
      datePosted: new Date().toISOString(),
    };
    console.log("newComment :", newComment);

    // setThreads((prevThreads) => ({
    //   ...prevThreads,
    //   [ticketId]: [newComment, ...(prevThreads[ticketId] || [])],
    // }));

    setCommentContent({ text: "", images: [] });
  };
  const handleCreateSubmit = (formData) => {
    handleSubmit(formData, navigate);
  };

  const handleSubmit = async (data) => {
    // const { projectMaster, AttachImages, resetForm } = get();
    const mappedValue = data.project;
    const filterProj = Project.find((data) => data.id === mappedValue.id);
    console.log(filterProj, data, projectMaster);

    const payload = {
      repo_Id: filterProj.repo_Id,
      title: data.title,
      description: data.description,
      // issuer_Id: "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      // created_On: "2025-11-29T10:08:41.581Z",
      // updated_On: "2025-11-29T10:08:41.581Z",
      project_Id: mappedValue.id,
      assignee_Id: data.assignedTo.id,
      due_Date: data.dueDate,
      // status: "string",
      issuelink_Id: "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      issue_Code: data.issue_Code,
      labels: (data.label || []).map((l) => ({
        label_Id: l.id,
        Issue_Id: null, // or some issueId
      })),
      tempReturns: {
        delete: "all",
        temps: (AttachImages || []).map((img) => ({
          fileName: img.name,
          publicUrl: img.url,
          localPath: img.LocalPath,
        })),
      },
    };
    console.log("payload :", payload);

    try {
      // Assuming PostIssues returns data you want to save in the store
      const result = await PostIssues(payload);
      // Example: Update the Zustand store after successful API call
      // useStore.setState({ issues: result });
      resetForm();
      console.log("Issue posted successfully:", result);
    } catch (error) {
      console.error("Error posting issue:", error);
    }
  };
  const handleviewfrom = () => {
    console.log("formdata :", formData);
  };

  const handleResetForm = () => {
    resetForm();
    setResetKey((prev) => prev + 1);
  };
  return (
    <>
      <DynamicForm
        key={resetKey}
        labels={ticketFields}
        formData={formData}
        formErrors={formErrors}
        onChange={(label, value) => handleInputChange(label, value)}
        onSubmit={handleCreateSubmit}
        onClose={handleCloseModal}
        // showCommentBar={true}
      />
      <button onClick={handleviewfrom}>view form </button>
      <button onClick={handleResetForm}>reset form </button>
      {/* <CommentBar/> */}
      {/* <FormBuilder
        tab={tab}
        setTab={setTab}
        commentContent={commentContent}
        setCommentContent={setCommentContent}
        onAddComment={onAddComment}
      /> */}
    </>
  );
};

export default CreateTicket;
