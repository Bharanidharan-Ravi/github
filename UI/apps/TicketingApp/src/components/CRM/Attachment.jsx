import { useState } from "react";
import { useAttachmentStore } from "./store/useAttachmentStore";

export default function AttachmentPreview() {

  const {
    attachments,
    uploadFiles,
    removeFile,
  } = useAttachmentStore();

  const [open, setOpen] = useState(false);
  const [activeIndex, setActiveIndex] = useState(0);

  // =========================
  // OPEN MODAL
  // =========================
  const openPreview = (index) => {
    setActiveIndex(index);
    setOpen(true);
  };

  // =========================
  // NAVIGATION
  // =========================
  const next = () => {
    setActiveIndex((prev) =>
      (prev + 1) % attachments.length
    );
  };

  const prev = () => {
    setActiveIndex((prev) =>
      prev === 0
        ? attachments.length - 1
        : prev - 1
    );
  };

  return (
    <div>

      {/* =========================
          FILE INPUT
      ========================= */}
      <input
        type="file"
        multiple
        accept="image/*"
        onChange={(e) =>
          uploadFiles(e.target.files)
        }
      />

      {/* =========================
          THUMBNAILS
      ========================= */}
      <div
        style={{
          display: "flex",
          gap: 10,
          flexWrap: "wrap",
          marginTop: 10,
        }}
      >
        {attachments.map((file, index) => (
          <div
            key={index}
            style={{
              position: "relative",
              width: 120,
              height: 120,
            }}
          >

            {/* Image */}
            <img
              src={file.previewUrl}
              alt=""
              onClick={() =>
                openPreview(index)
              }
              style={{
                width: "100%",
                height: "100%",
                objectFit: "cover",
                borderRadius: 6,
                border: "1px solid #ccc",
                cursor: "pointer",
              }}
            />

            {/* Remove */}
            <button
              onClick={() =>
                removeFile(index)
              }
              style={{
                position: "absolute",
                top: -6,
                right: -6,
                background: "red",
                color: "#fff",
                border: "none",
                borderRadius: "50%",
                width: 22,
                height: 22,
                cursor: "pointer",
              }}
            >
              ✕
            </button>

          </div>
        ))}
      </div>

      {/* =========================
          MODAL CAROUSEL
      ========================= */}
      {open && (
        <div style={modalOverlay}>

          {/* Close */}
          <button
            style={closeBtn}
            onClick={() => setOpen(false)}
          >
            ✕
          </button>

          {/* Prev */}
          <button
            style={navLeft}
            onClick={prev}
          >
            ‹
          </button>

          {/* Image */}
          <img
            src={
              attachments[activeIndex]
                ?.previewUrl
            }
            alt=""
            style={modalImage}
          />

          {/* Next */}
          <button
            style={navRight}
            onClick={next}
          >
            ›
          </button>

        </div>
      )}

    </div>
  );
}
