import React from "react";
import ImageRender from "../ImageRender/ImageRender";
import "./ThreadCard.css";
import { HtmlRenderer } from "../../../../Shared/helper/utilies";

const ThreadCard = ({ thread, onImageClick }) => {
    const {
        commentText,
        createdBy,
        commentedAt,
        status,
        attachment_JSON = [],
    } = thread;

    const formattedDate = commentedAt ? new Date(commentedAt).toLocaleString() : "";

    return (
        <article className="ticket-thread-card">
            <header className="ticket-thread-header">
                <div className="ticket-thread-user">
                    <div className="ticket-thread-avatar">
                        {createdBy?.[0]?.toUpperCase()}
                    </div>
                    <div>
                        <div className="ticket-thread-user-row">
                            <span className="ticket-thread-username">{createdBy}</span>
                        </div>
                        <div className="ticket-thread-meta">{formattedDate}</div>
                    </div>
                </div>
            </header>
            <div className="ticket-thread-body">
                {/* <p className="ticket-thread-text">{commentText}</p> */}
                 <HtmlRenderer html={commentText} />
                {attachment_JSON?.length > 0 && (
                    <div className="ticket-thread-attachments">
                        <ImageRender
                            attachment={attachment_JSON}
                            onImageClick={onImageClick}
                        />
                    </div>
                )}
            </div>
        </article>
    );
};

export default ThreadCard;