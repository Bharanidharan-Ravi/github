import React, {
  useState,
  useRef,
  useEffect,
  useCallback
} from "react";

import { useTicketStore } from "../../TicketStore/TicketStore";
import { postImage } from "shared-store";

import QuillEditor from "./QuillEditor1";

const MAX_FILES = 10;
const MAX_FILE_SIZE = 10 * 1024 * 1024;

const IMAGE_TYPES = ["image/jpeg", "image/png", "image/jpg"];
const ATTACH_TYPES = [
  ...IMAGE_TYPES,
  "application/pdf",
  "application/msword",
  "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
  "application/vnd.ms-excel",
  "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
];

export default function CommentBar1({ labels = [] }) {
  const {
    formData,
    handleInputChange,
    addImages,
    AttachImages,
    userList,
    setremoveImage
  } = useTicketStore();

  const quillRef = useRef(null); // important: holds actual Quill instance
  const perviousHtmlRef = useRef(null);
  const [draggingOuter, setDraggingOuter] = useState(false);
  const [inlineDragging, setInlineDragging] = useState(false);
  const [dropLineTop, setDropLineTop] = useState(null);
  const [ghostImageUrl, setGhostImageUrl] = useState(null);
  const [ghostPos, setGhostPos] = useState({ x: 0, y: 0 });

  const [showMentionMenu, setShowMentionMenu] = useState(false);
  const [showLabelMenu, setShowLabelMenu] = useState(false);

  // 🔥 Store Quill instance
  const setQuillInstance = (instance) => {
    quillRef.current = instance;
  };
  const removeInlineImage = (imageUrl) => {
    const found = AttachImages.find(img =>
      img.tempUrl === imageUrl || img.finalUrl === imageUrl
    );
    setremoveImage(found);

    const editor = quillRef.current;
    if (!editor) return;

    const delta = editor.getContents();
    const newDelta = new Delta();

    delta.ops.forEach(op => {
      if (op.insert && op.insert.image === imageUrl) {

      } else {
        newDelta.insert(op.insert, op.attributes);
      }
    });

    editor.setContents(newDelta);

    console.log("image removed");

  }
  useEffect(() => {
    const quill = quillRef.current;
    if (!quill) return;

    const editor = quill;

    const handleDeleteImage = () => {
      const prevHtml = perviousHtmlRef.current;
      const newHtml = editor.root.innerHtml;
console.log("prevHtml:", prevHtml, newHtml);

      const pervImages = Array.from(prevHtml.matchAll(/<img[^>]+src="([^"]+)"/g)).map(m => m[1]);
      const newImages = Array.from(newHtml.matchAll(/<img[^>]+src="([^"]+)"/g)).map(m => m[1]);

      const removeImages = pervImages.filter(src => !newImages.includes(src));

      if (removeImages.length > 0) {
        removeImages.forEach(src => {
          removeInlineImage(src);
        });
      }

      perviousHtmlRef.current = newHtml;
    };

    editor.on("text-change", handleDeleteImage);
    return () => editor.off("text-change", handleDeleteImage);
  }, [removeInlineImage]);


  // ------------------------------ HTML Change Handler ---------------------------
  const handleEditorChange = (html) => {
    handleInputChange("description", html);
  };

  // Insert image URL into Quill inline
  const insertImageIntoQuill = (url) => {
    const editor = quillRef.current;
    if (!editor) return;

    const range = editor.getSelection(true) || {
      index: editor.getLength(),
      length: 0,
    };
    console.log("rande :", range.index, "image :", url);

    editor.insertEmbed(range.index, "image", url, "user");
    editor.setSelection(range.index + 1);
  };

  // ------------------------------ Upload Logic --------------------------------
  const uploadFile = async (file) => {
    const res = await postImage(file);

    return {
      tempUrl: res.publicUrl,
      finalUrl: res.finalUrl,
      name: res.fileName,
      localPath: res.localPath,

    };
  };

  // OUTSIDE container → attachments
  const handleAttachmentUpload = useCallback(
    async (file) => {
      if (!ATTACH_TYPES.includes(file.type)) return;
      if (file.size > MAX_FILE_SIZE) return;
      if (AttachImages.length >= MAX_FILES) return;

      const dup = AttachImages.some((f) => f.name === file.name);
      if (dup) return;

      const uploaded = await uploadFile(file);
      addImages(uploaded);
    },
    [AttachImages]
  );

  // SHIFT inside editor → inline image
  const handleInlineImageUpload = useCallback(
    async (file) => {
      if (!IMAGE_TYPES.includes(file.type)) return;
      if (file.size > MAX_FILE_SIZE) return;
      if (AttachImages.length >= MAX_FILES) return;

      const dup = AttachImages.some((f) => f.name === file.name);
      if (dup) return;

      const uploaded = await uploadFile(file);
      // addImages(uploaded);

      // insert temp image now
      insertImageIntoQuill(uploaded.tempUrl);
    },
    [AttachImages]
  );

  // ------------------------------ OUTER DRAG -----------------------------------
  const handleOuterDragOver = (e) => {
    if (e.shiftKey) return;
    e.preventDefault();
    setDraggingOuter(true);
  };

  const handleOuterDragLeave = (e) => {
    e.preventDefault();
    setDraggingOuter(false);
  };

  const handleOuterDrop = async (e) => {
    if (e.shiftKey) return;
    e.preventDefault();
    setDraggingOuter(false);

    const files = Array.from(e.dataTransfer.files || []);
    for (const f of files) {
      await handleAttachmentUpload(f);
    }
  };

  // ------------------------------ INLINE DRAG (Notion Style) -------------------

  useEffect(() => {
    const editor = quillRef.current;
    if (!editor) return;

    const root = editor.root;
    const debug = (name, e) => {
      console.log(
        `%c[DRAG EVENT] &{name}`,
        "color: #22c55e; font-weight: bold",
        {
          shiftKey: e.shiftKey,
          type: e.type,
          files: e.dataTransfer?.files?.length ?? 0,
          x: e.clientX,
          y: e.clientY
        }
      );
    };
    // drag over inside Quill
    const onDragOver = (e) => {
      debug("dragover", e);
      if (!e.shiftKey) return;
      e.preventDefault();
      e.stopPropagation();

      setInlineDragging(true);
      const { clientX, clientY } = e;
      setGhostPos({ x: clientX, y: clientY });

      const dtFiles = e.dataTransfer?.files;
      if (dtFiles && dtFiles.length > 0 && !ghostImageUrl) {
        const f = dtFiles[0];
        if (f.type.startsWith("image/")) {
          setGhostImageUrl(URL.createObjectURL(f));
        }
      }

      // show drop line where cursor would go
      const selection = editor.getSelection() || {
        index: editor.getLength(),
        length: 0,
      };
      const bounds = editor.getBounds(selection.index);
      console.log("selection :", selection, "bounds :", bounds);

      setDropLineTop({
        top: bounds.top,
        left: bounds.left,
        height: bounds.height,
      });
    };

    const onDragLeave = (e) => {
      debug("dragLeave ", e);
      e.preventDefault();
      e.stopPropagation();
      setInlineDragging(false);
      setDropLineTop(null);
      if (ghostImageUrl) {
        URL.revokeObjectURL(ghostImageUrl);
        setGhostImageUrl(null);
      }
    };

    const onDrop = async (e) => {
      debug("dragDrop", e);
      if (!e.shiftKey) return;
      e.preventDefault();
      e.stopPropagation();

      setInlineDragging(false);
      setDropLineTop(null);

      if (ghostImageUrl) {
        URL.revokeObjectURL(ghostImageUrl);
        setGhostImageUrl(null);
      }

      const files = Array.from(e.dataTransfer.files || []);
      for (const f of files) {
        await handleInlineImageUpload(f);
      }
    };

    root.addEventListener("dragover", onDragOver);
    root.addEventListener("dragleave", onDragLeave);
    root.addEventListener("drop", onDrop);

    return () => {
      root.removeEventListener("dragover", onDragOver);
      root.removeEventListener("dragleave", onDragLeave);
      root.removeEventListener("drop", onDrop);
    };
  }, [ghostImageUrl, handleInlineImageUpload]);

  // ------------------------------ Mention + Label ------------------------------

  const insertHtml = (html) => {
    const editor = quillRef.current;
    if (!editor) return;

    const range = editor.getSelection(true) || {
      index: editor.getLength(),
      length: 0,
    };

    editor.clipboard.dangerouslyPasteHTML(range.index, html);
    editor.setSelection(range.index + html.length, 0);
  };

  const handleSelectUser = (u) => {
    const display = u.displayName || u.name || u.username || "";
    insertHtml(`<strong>@${display}</strong> `);
    setShowMentionMenu(false);
  };

  const handleSelectLabel = (l) => {
    insertHtml(
      `<span style="background:#fef3c7;padding:2px 4px;border-radius:4px;">#${l.label_TITLE}</span> `
    );
    setShowLabelMenu(false);
  };

  // ------------------------------ DELETE ATTACHMENT -----------------------------

  const handleDelete = async (img) => {
    try {
      setremoveImage(img);
    } catch (err) {
      console.error(err);
    }
  };

  // ------------------------------ RENDER ----------------------------------------

  return (
    <div
      onDragOver={handleOuterDragOver}
      onDragLeave={handleOuterDragLeave}
      onDrop={handleOuterDrop}
      style={{
        border: "1px solid #d1d5db",
        padding: 12,
        borderRadius: 8,
        background: "#fff",
        maxWidth: 700,
        margin: "10px auto",
        position: "relative",
      }}
    >
      {/* Attachments highlight */}
      {draggingOuter && !inlineDragging && (
        <div
          style={{
            position: "absolute",
            inset: 0,
            borderRadius: 8,
            border: "2px dashed #93c5fd",
            background: "rgba(219,234,254,0.4)",
            pointerEvents: "none",
          }}
        />
      )}

      {/* Mention and Label buttons */}
      <div style={{ display: "flex", gap: 8, marginBottom: 8 }}>
        <button type="button" onClick={() => setShowMentionMenu((s) => !s)}>
          @ Mention
        </button>

        <button type="button" onClick={() => setShowLabelMenu((s) => !s)}>
          # Label
        </button>
      </div>

      {/* Mention Menu */}
      {showMentionMenu && userList.length > 0 && (
        <div
          style={{
            position: "absolute",
            top: 42,
            left: 10,
            zIndex: 50,
            background: "#fff",
            border: "1px solid #e5e7eb",
            borderRadius: 6,
            padding: 6,
          }}
        >
          {userList.map((u) => (
            <div
              key={u.id}
              style={{ padding: 4, cursor: "pointer" }}
              onClick={() => handleSelectUser(u)}
            >
              @{u.displayName || u.name || u.username}
            </div>
          ))}
        </div>
      )}

      {/* Label Menu */}
      {showLabelMenu && labels.length > 0 && (
        <div
          style={{
            position: "absolute",
            top: 42,
            left: 100,
            zIndex: 50,
            background: "#fff",
            border: "1px solid #e5e7eb",
            borderRadius: 6,
            padding: 6,
          }}
        >
          {labels.map((l) => (
            <div
              key={l.label_Id}
              style={{ padding: 4, cursor: "pointer" }}
              onClick={() => handleSelectLabel(l)}
            >
              #{l.label_TITLE}
            </div>
          ))}
        </div>
      )}

      {/* Quill Editor */}
      <div style={{ position: "relative" }}>
        <QuillEditor
          value={formData.description}
          onChange={handleEditorChange}
          setQuillInstance={setQuillInstance}
        />

        {/* Notion Drop Line */}
        {inlineDragging && dropLineTop !== null && (
          <div
            style={{
              position: "absolute",
              left: 0,
              right: 0,
              height: 2,
              top: dropLineTop,
              background: "#3b82f6",
              pointerEvents: "none",
            }}
          />
        )}

        {/* Ghost image preview */}
        {inlineDragging && ghostImageUrl && (
          <img
            src={ghostImageUrl}
            alt="preview"
            style={{
              position: "fixed",
              top: ghostPos.y + 10,
              left: ghostPos.x + 10,
              width: 150,
              height: "auto",
              opacity: 0.6,
              borderRadius: 6,
              boxShadow: "0 4px 10px rgba(15,23,42,0.25)",
              pointerEvents: "none",
            }}
          />
        )}
      </div>

      {/* Attachments list */}
      <div style={{ display: "flex", gap: 10, flexWrap: "wrap", marginTop: 8 }}>
        {console.log("img :", AttachImages)}
        {AttachImages.map((img, i) =>

          // img.type.startsWith("image/") ? (
          <div key={i} style={{ position: "relative" }}>
            <img
              src={img.finalUrl || img.tempUrl}
              alt=""
              style={{ width: 70, height: 70, borderRadius: 6 }}
            />
            <button
              type="button"
              onClick={() => handleDelete(img)}
              style={{
                position: "absolute",
                top: -6,
                right: -6,
                borderRadius: "50%",
                background: "rgba(0,0,0,0.7)",
                color: "#fff",
                width: 18,
                height: 18,
                border: "none",
                cursor: "pointer",
                fontSize: 10,
              }}
            >
              ✕
            </button>
          </div>
          // ) : (
          //   <div
          //     key={i}
          //     style={{
          //       padding: "4px 8px",
          //       borderRadius: 4,
          //       border: "1px solid #e5e7eb",
          //       fontSize: 12,
          //     }}
          //   >
          //     {img.name}
          //   </div>
          // )
        )}
      </div>
    </div>
  );
}
