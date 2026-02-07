import React, { useCallback, useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { PostThread, GetThreadList, useCustomStore } from 'shared-store';
import { useTicketStore } from "../TicketStore/TicketStore";
import "../Css/TicketDetails.css";
import Lightbox from "../Components/LightBox/LightBox";
import ImageRender from "../Components/ImageRender/ImageRender";
import CommentBar from "../../../Shared/commentBar1";
import ThreadCard from "../Components/ThreadCard/ThreadCard";
import { HtmlRenderer } from "../../../Shared/helper/utilies";


const TicketDetails = () => {
    const { ticketId } = useParams();
    const ThreadList = useCustomStore(state => state.ThreadList);
    const { formData, AttachImages, setThreadList, threadList, threadIssuesData, 
        setThreadIssuesData, resetForm } = useTicketStore();
    const [lightboxIndex, setLightboxIndex] = useState({
        isOpen: false,
        attachments: [],
        index: 0,
    });

    const openLightBox = useCallback((attachments, index) => {
        if (!attachments || !attachments.length) return;
        setLightboxIndex({
            isOpen: true,
            attachments,
            index,
        });
    }, []);

    const closeLightBox = useCallback(() => {
        setLightboxIndex((perv) => ({
            ...perv,
            isOpen: false,
        }));
    }, []);

    const setLightboxIndexState = useCallback((updater) => {
        setLightboxIndex((prev) => ({
            ...prev,
            index: typeof updater === "function" ? updater(prev.index) : updater,
        }));
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
        setThreadList(result.threadData);
        setThreadIssuesData(result.issuesData);
        resetForm();
    }
    return (
        <div className="ticketdetail-main container">
            <p>new details</p>
            {threadIssuesData && (
                <div className="ticketdetail-header">
                    <div className="ticketdetail-header-title-row">
                        <h2 className="m-0 ticketdetail-title">{threadIssuesData.issue_Title}</h2>
                    </div>
                    <div className="ticket-labels">
                        {threadIssuesData.labels_JSON?.map((label, i) => (
                            <span key={i} className={`ticket-label`}
                                style={{ "--label-color": label.labeL_COLOR }}
                            >
                                {label.labeL_TITLE}
                            </span>
                        ))}
                    </div>


                    <div className="ticketdetail-detail">
                        <div className="ticketdetail-desc">
                            {/* <p>{threadIssuesData.description}</p> */}
                            <HtmlRenderer html={threadIssuesData.description} />
                        </div>
                        <ImageRender
                            attachment={threadIssuesData?.attachment_JSON}
                            onImageClick={(index) =>
                                openLightBox(threadIssuesData.attachment_JSON, index)
                            }
                        // setLightboxIndex={setLightboxIndex}
                        />
                    </div>
                </div>
            )}

            <div className="repo-body">
                <ul className="ticket-thread-list">
                    {Array.isArray(threadList) && threadList?.map((threads) => (
                        <li key={threads.threadId} className={`ticketDetails ${threads.status}`}  >
                            <ThreadCard
                                thread={threads}
                                onImageClick={(index) =>
                                    openLightBox(threads.attachment_JSON || [], index)
                                }
                            />
                            {/* <div className="ticket-header">
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
                            </div> */}
                        </li>
                    ))}
                </ul>
                <div className="">
                    <CommentBar />
                    <button
                        className="ticket-comment-button"
                        onClick={handlethreadSubmit}>
                        Comment
                    </button>
                </div>
            </div>

            {lightboxIndex.isOpen &&
                lightboxIndex.attachments &&
                lightboxIndex.attachments.length > 0 && (
                    <Lightbox
                        lightboxIndex={lightboxIndex.index}
                        setLightboxIndex={setLightboxIndexState}
                        attachment={lightboxIndex.attachments}
                        onclose={closeLightBox}
                    />
                )}
        </div>

    );
};
export default TicketDetails;