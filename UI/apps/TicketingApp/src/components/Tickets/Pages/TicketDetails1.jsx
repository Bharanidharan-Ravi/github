import React, { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import CommentBar from "../../../Shared/commentBar1";
import { PostThread, GetThreadList, useCustomStore } from 'shared-store';
import { useTicketStore } from "../TicketStore/TicketStore";
import { Folder } from "lucide-react";
import { ChevronRight } from "lucide-react";
import "../Css/TicketDetails.css";
import Lightbox from "../Components/LightBox/LightBox";
import ImageRender from "../Components/ImageRender/ImageRender";

const TicketDetails1 = () => {
  const { ticketId } = useParams();
  const ThreadList = useCustomStore(state => state.ThreadList);
  const { formData, AttachImages, setThreadList, threadList, threadIssuesData, setThreadIssuesData } = useTicketStore();
  const [lightboxIndex, setLightboxIndex] = useState(null);

  useEffect(() => {
    const handleKey = (e) => {
      if (e.key === "Escape") setLightboxIndex(null);
    };
    window.addEventListener("keydown", handleKey);
    return () => window.removeEventListener("keydown", handleKey);
  }, []);

  useEffect(() => {
    const fetchData = async () => {
      if (!threadList?.length) {
        const result = await GetThreadList({ ticketId });
        setThreadList(result.threadData);
        setThreadIssuesData(result.issuesData);
      } else {
        setThreadList(ThreadList.threadData);
        setThreadIssuesData(ThreadList.issuesData);
      }
    };
    if (!threadList || threadList.length === 0 || !threadIssuesData) {
      fetchData();
    }
  }, []);

  const handlethreadSubmit = async () => {
    const payload = {
      thread: {
        issue_Id: threadIssuesData.issue_Id,               // <-- pass your real variable here
        issueTitle: threadIssuesData.issue_Title,          // <-- your variable
        commentText: formData.description, 
      },
      tempReturns: {
        delete: "All",             // <-- your own variable
        temps: AttachImages.map(file => ({
          fileName: file.name,
          publicUrl: file.url,
          localPath: file.LocalPath
        }))
      }
    };

    console.log('payload:', payload);
    const result = await PostThread(payload);
    console.log("result :", result);

  }
  return (
    <div className="repodetail-main container">
      {threadIssuesData && (
        <div className="ticketdetail-header">
          <div className="d-flex">
            <h2 className="m-0">{threadIssuesData.issue_Title}</h2>

            <div className="ticket-labels">
              {threadIssuesData.labels_JSON?.map((label, i) => (
                <span key={i} className={`ticket-label`}
                  style={{ "--label-color": label.labeL_COLOR }}
                >
                  {label.labeL_TITLE}
                </span>
              ))}
            </div>
          </div>

          <div className="ticketdetail-detail">
            <div className="ticketdetail-desc">
              <p>{threadIssuesData.description}</p>
            </div>
            <ImageRender
              attachment={threadIssuesData?.attachment_JSON}
              setLightboxIndex={setLightboxIndex}
            />
          </div>
        </div>
      )}

      <div className="repo-body">
        <ul className="ticket-list">
          {Array.isArray(threadList) && threadList?.map((threads) => (
            <li key={threads.threadId} className={`ticketDetails ${threads.status}`}  >
              <div className="ticket-header">
                <div>
                  <span className="Repo-title">{threads.commentText}</span>
                </div>
                <div>
                  <span>{threads.commentedAt}</span>
                  <span>{threads.createdBy}</span>
                </div>
                <ImageRender
                  attachment={threads?.attachment_JSON}
                  setLightboxIndex={setLightboxIndex} />
              </div>
            </li>
          ))}
        </ul>

        <CommentBar />
        <button onClick={handlethreadSubmit}>Comment</button>
      </div>

      {lightboxIndex !== null && (
        <Lightbox
          lightboxIndex={lightboxIndex}
          setLightboxIndex={setLightboxIndex}
          attachment={threadIssuesData.attachment_JSON}
        />
      )}
    </div>

  );
};
export default TicketDetails1;