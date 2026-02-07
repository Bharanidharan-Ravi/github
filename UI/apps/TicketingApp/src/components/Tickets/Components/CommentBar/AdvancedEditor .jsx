import React, { useState, useEffect, useRef } from "react";
import {
  useEditor,
  EditorContent,
  ReactNodeViewRenderer,
  NodeViewWrapper,
} from "@tiptap/react";
import { Node, mergeAttributes } from "@tiptap/core"; // Needed for custom extensions
import StarterKit from "@tiptap/starter-kit";
import { Underline } from "@tiptap/extension-underline";
import { Image } from "@tiptap/extension-image";
import { Mention } from "@tiptap/extension-mention";
import { Placeholder } from "@tiptap/extension-placeholder";
import { Table } from "@tiptap/extension-table";
import { TableRow } from "@tiptap/extension-table-row";
import { TableCell } from "@tiptap/extension-table-cell";
import { TableHeader } from "@tiptap/extension-table-header";
import { Link } from "@tiptap/extension-link";
import { createSuggestion } from "./suggestion";
import { postImage } from "shared-store";

// Icons
import {
  FaBold,
  FaItalic,
  FaUnderline,
  FaListUl,
  FaListOl,
  FaUndo,
  FaRedo,
  FaImage,
  FaPlus,
  FaFileAlt,
  FaHeading,
  FaTable,
  FaPaperclip,
  FaEye,
  FaPen,
} from "react-icons/fa";
import "./AdvancedEditor.css";

// --- DATA ---
const userList = [
  { id: "u1", name: "Ravi Kumar" },
  { id: "u2", name: "Ram Prakash" },
  { id: "u3", name: "Bharathi" },
  { id: "u4", name: "Sathish Kumar" },
];

const labelList = [
  { id: "l1", name: "frontend", color: "#0ea5e9" },
  { id: "l2", name: "backend", color: "#8b5cf6" },
  { id: "l3", name: "urgent", color: "#ef4444" },
  { id: "l4", name: "api", color: "#14b8a6" },
];

// --- MOCK UPLOAD ---
export const uploadImage = async (file) => {
  // Replace with your real upload API
  return new Promise((resolve) =>
    setTimeout(() => resolve(URL.createObjectURL(file)), 500)
  );
};

// ----------------------------------------------------------------------
// 1. NEW CUSTOM EXTENSION: FILE ATTACHMENT (The "Windows Style" Chip)
// ----------------------------------------------------------------------

const FileAttachmentComponent = ({ node, selected }) => {
  return (
    // CHANGE 1: Use 'as="span"' to force it to sit inline like text
    <NodeViewWrapper as="span" className="node-view-wrapper">
      <a
        href={node.attrs.src}
        target="_blank"
        rel="noopener noreferrer"
        className={`file-attachment-chip ${selected ? "selected" : ""}`}
        contentEditable={false}
        // CHANGE 2: Ensure the inner link is also inline-flex
        style={{ display: "inline-flex" }}
      >
        <span className="file-icon">
          <FaFileAlt />
        </span>
        <span className="file-name">{node.attrs.fileName}</span>
      </a>
    </NodeViewWrapper>
  );
};

const FileAttachment = Node.create({
  name: "fileAttachment",
  group: "inline",
  inline: true,
  selectable: true,
  atom: true, // Treated as a single unit

  addAttributes() {
    return {
      src: { default: null },
      fileName: { default: "file" },
    };
  },

  parseHTML() {
    return [{ tag: 'a[data-type="file-attachment"]' }];
  },

  //   renderHTML({ HTMLAttributes }) {
  //     return ['a', mergeAttributes(HTMLAttributes, { 'data-type': 'file-attachment', target: '_blank', class: 'file-attachment-link' })];
  //   },
  // NEW (FIXES PREVIEW)
  renderHTML({ node, HTMLAttributes }) {
    // We manually build the HTML structure: <a> <span>Icon</span> <span>Name</span> </a>
    return [
      "a",
      mergeAttributes(HTMLAttributes, {
        class: "file-attachment-chip", // Use the SAME class as the React component
        href: node.attrs.src,
        target: "_blank",
        rel: "noopener noreferrer",
      }),
      ["span", { class: "file-icon", style: "margin-right: 5px;" }, "📄"], // Inner Icon
      ["span", { class: "file-name" }, node.attrs.fileName], // Inner Text
    ];
  },

  addNodeView() {
    return ReactNodeViewRenderer(FileAttachmentComponent);
  },
});

// ----------------------------------------------------------------------
// 2. CUSTOM EXTENSION: IMAGE CHIP (For Editor Mode)
// ----------------------------------------------------------------------
const ImageNodeView = (props) => {
  const { node, selected } = props;
  return (
    <NodeViewWrapper as="span" className="node-view-wrapper">
      <div
        className={`attachment-pill ${selected ? "selected" : ""}`}
        style={{ display: "inline-flex" }} // Force horizontal layout
      >
        <span className="doc-icon">🖼️</span>
        <span style={{ fontWeight: 500 }}>{node.attrs.alt || "Image"}</span>
      </div>
    </NodeViewWrapper>
  );
};

const CustomImage = Image.extend({
  addNodeView() {
    return ReactNodeViewRenderer(ImageNodeView);
  },
});

// ----------------------------------------------------------------------
// 3. TABLE SELECTOR COMPONENT
// ----------------------------------------------------------------------
const TableSelector = ({ onSelect }) => {
  const [hovered, setHovered] = useState({ r: 0, c: 0 });
  return (
    <div className="table-selector-popup">
      <div className="table-grid-label">
        {hovered.r > 0 ? `${hovered.c} x ${hovered.r}` : "Insert Table"}
      </div>
      <div
        className="table-grid"
        onMouseLeave={() => setHovered({ r: 0, c: 0 })}
      >
        {[...Array(5)].map((_, row) => (
          <div key={row} className="table-grid-row">
            {[...Array(5)].map((_, col) => (
              <div
                key={col}
                className={`table-grid-cell ${
                  col < hovered.c && row < hovered.r ? "active" : ""
                }`}
                onMouseEnter={() => setHovered({ r: row + 1, c: col + 1 })}
                onMouseDown={(e) => {
                  e.preventDefault();
                  onSelect(row + 1, col + 1);
                }}
              />
            ))}
          </div>
        ))}
      </div>
    </div>
  );
};

// ----------------------------------------------------------------------
// 4. MAIN EDITOR COMPONENT
// ----------------------------------------------------------------------
const AdvancedEditor = ({ onChange, initialContent = "" }) => {
  const fileInputRef = useRef(null);
  const [floatingMenu, setFloatingMenu] = useState({
    show: false,
    x: 0,
    y: 0,
    isOpen: false,
  });
  const [showTableSelector, setShowTableSelector] = useState(false);
  const [isUploading, setIsUploading] = useState(false);
  const [isPreviewMode, setIsPreviewMode] = useState(false);

  const editor = useEditor({
    content: initialContent,
    extensions: [
      StarterKit.configure({
        history: true,
      }),
      Underline,
      CustomImage, // Use our chip view for images in editor
      FileAttachment, // Use our new file attachment node
      Link.configure({ openOnClick: false }),
      Table.configure({ resizable: true }),
      TableRow,
      TableHeader,
      TableCell,
      Placeholder.configure({
        placeholder: "Type @ for users, # for labels...",
      }),

      Mention.configure({
        HTMLAttributes: { class: "mention-user" },
        suggestion: createSuggestion(userList),
      }),

      Mention.extend({
        name: "labelMention",
        addAttributes() {
          return {
            id: { default: null },
            label: { default: null },
            color: { default: null },
          };
        },
        parseHTML() {
          return [{ tag: 'span[data-type="labelMention"]' }];
        },
        renderHTML({ node }) {
          return [
            "span",
            {
              "data-type": "labelMention",
              class: "mention-label",
              style: `background-color: ${
                node.attrs.color || "#666"
              }; color: white; padding: 2px 6px; border-radius: 4px; display: inline-block; box-decoration-break: clone;`,
            },
            `#${node.attrs.label ?? node.attrs.id}`,
          ];
        },
      }).configure({
        suggestion: { ...createSuggestion(labelList), char: "#" },
      }),
    ],
    onUpdate: ({ editor }) => {
      if (onChange) onChange("description", editor.getHTML());
    },
    editorProps: {
      handleClick: (view, pos, event) => {
        if (event.target.classList.contains("mention-user")) {
          alert(
            `Navigating to profile: ${event.target.getAttribute("data-id")}`
          );
          return true;
        }
        return false;
      },
      // DRAG & DROP HANDLER
      handleDrop: function (view, event, slice, moved) {
        if (
          !moved &&
          event.dataTransfer &&
          event.dataTransfer.files &&
          event.dataTransfer.files.length > 0
        ) {
          event.preventDefault(); // Stop browser from opening files

          const files = Array.from(event.dataTransfer.files);
          setIsUploading(true);

          // We use a loop to process all dropped files
          // Note: using async inside handleDrop requires careful handling,
          // but for this UI simple iteration works best.
          (async () => {
            for (const file of files) {
              // const url = await uploadImage(file);
              const response = await postImage(file);
              const url = response.publicUrl;
              const { schema } = view.state;

              // We insert at the current selection end or mouse position
              // For drag and drop, Tiptap usually handles pos, but we are overriding it.
              // Simpler approach: Append to current cursor or drop location.

              // Re-query coordinates for every file (or just append)
              const coordinates = view.posAtCoords({
                left: event.clientX,
                top: event.clientY,
              });
              const pos = coordinates
                ? coordinates.pos
                : view.state.selection.from;

              if (file.type.startsWith("image/")) {
                const node = schema.nodes.image.create({
                  src: url,
                  alt: file.name,
                });
                const transaction = view.state.tr.insert(pos, node);
                view.dispatch(transaction);
              } else {
                const node = schema.nodes.fileAttachment.create({
                  src: url,
                  fileName: file.name,
                });
                const transaction = view.state.tr.insert(pos, node);
                view.dispatch(transaction);
                // Insert a space after
                view.dispatch(view.state.tr.insertText(" ", pos + 1));
              }
            }
            setIsUploading(false);
          })();
          return true; // Signal that we handled the drop
        }
        return false;
      },
    },
  });

  // --- 2. CRITICAL FIX: SYNC CONTENT WHEN PARENT RESETS ---
  useEffect(() => {
    // If the editor is ready, and the Parent's content (initialContent)
    // is different from the Editor's content, force an update.
    if (editor && initialContent !== editor.getHTML()) {
      // Use 'emitUpdate: false' so we don't trigger another onChange loop
      editor.commands.setContent(initialContent, { emitUpdate: false });
    }
  }, [initialContent, editor]);
  
  // Floating Menu Logic
  useEffect(() => {
    if (!editor) return;
    const updateFloatingMenu = () => {
      const { selection } = editor.state;
      const { $anchor } = selection;
      const isLineEmpty = $anchor.parent.content.size === 0;

      if (isLineEmpty && !isPreviewMode) {
        const pos = editor.view.coordsAtPos($anchor.pos);
        const editorRect = editor.view.dom.getBoundingClientRect();
        setFloatingMenu((prev) => ({
          show: true,
          x: 20,
          y: pos.top - editorRect.top,
          isOpen: prev.isOpen,
        }));
      } else {
        setFloatingMenu((prev) => ({ ...prev, show: false, isOpen: false }));
      }
    };
    editor.on("selectionUpdate", updateFloatingMenu);
    return () => editor.off("selectionUpdate", updateFloatingMenu);
  }, [editor, isPreviewMode]);

  // Handlers
  const handleFileClick = () => fileInputRef.current.click();

  const handleFileChange = async (event) => {
    const files = Array.from(event.target.files);
    if (!files || files.length === 0) return;

    setIsUploading(true);

    try {
      // 1. Upload ALL files in parallel (Faster & Cleaner)
      const uploadedFiles = await Promise.all(
        files.map(async (file) => {
          // const url = await uploadImage(file); // Your API call
          const response = await postImage(file);
          console.log("response :", response);
          var url = response.publicUrl;
          return { file, url };
        })
      );

      // 2. Create a list of Tiptap Nodes to insert
      const contentToInsert = uploadedFiles.flatMap(({ file, url }) => {
        if (file.type.startsWith("image/")) {
          return [
            {
              type: "image",
              attrs: { src: url, alt: file.name },
            },
            { type: "text", text: " " }, // Optional: Space after image
          ];
        } else {
          return [
            {
              type: "fileAttachment", // Your custom extension name
              attrs: { src: url, fileName: file.name },
            },
            { type: "text", text: " " }, // Space after attachment so they don't stick
          ];
        }
      });

      // 3. Insert EVERYTHING in one single transaction
      // This guarantees they all append correctly without overwriting
      if (contentToInsert.length > 0) {
        editor.chain().focus().insertContent(contentToInsert).run();
      }
    } catch (error) {
      console.error("Upload failed", error);
      alert("Something went wrong while uploading.");
    } finally {
      setIsUploading(false);
      setFloatingMenu((prev) => ({ ...prev, isOpen: false }));
      if (event.target) event.target.value = ""; // Reset input to allow re-uploading same file
    }
  };
  const insertTable = (rows, cols) => {
    editor
      .chain()
      .focus()
      .insertTable({ rows, cols, withHeaderRow: true })
      .run();
    setShowTableSelector(false);
  };

  const ToolbarButton = ({ onClick, isActive, icon, label, disabled }) => (
    <button
      type="button"
      onMouseDown={(e) => {
        e.preventDefault(); // Prevent the default behavior (e.g., form submission)
        e.stopPropagation(); // Stop the event from propagating to the parent (form)
        if (!disabled) {
          onClick(); // Call the passed onClick handler (editor chain action)
        }
      }}
      className={`toolbar-btn ${isActive ? "is-active" : ""}`}
      title={label}
      disabled={disabled}
      style={{ opacity: disabled ? 0.5 : 1 }}
    >
      {icon}
    </button>
  );

  if (!editor) return null;

  return (
    <div className="editor-wrapper">
      <div className="main-toolbar">
        <ToolbarButton
          onClick={() => setIsPreviewMode(!isPreviewMode)}
          isActive={isPreviewMode}
          icon={isPreviewMode ? <FaPen /> : <FaEye />}
          label={isPreviewMode ? "Edit Mode" : "Preview Mode"}
        />
        <div className="divider"></div>

        {!isPreviewMode && (
          <>
            <ToolbarButton
              onClick={() =>
                editor.chain().focus().toggleHeading({ level: 1 }).run()
              }
              isActive={editor.isActive("heading", { level: 1 })}
              icon={<span>H1</span>}
              label="Heading 1"
            />
            <ToolbarButton
              onClick={() =>
                editor.chain().focus().toggleHeading({ level: 2 }).run()
              }
              isActive={editor.isActive("heading", { level: 2 })}
              icon={<span>H2</span>}
              label="Heading 2"
            />
            <ToolbarButton
              onClick={() =>
                editor.chain().focus().toggleHeading({ level: 3 }).run()
              }
              isActive={editor.isActive("heading", { level: 3 })}
              icon={<span>H3</span>}
              label="Heading 3"
            />
            <div className="divider"></div>

            <ToolbarButton
              onClick={() => editor.chain().focus().toggleBold().run()}
              isActive={editor.isActive("bold")}
              icon={<FaBold />}
              label="Bold"
            />

            <ToolbarButton
              onClick={() => editor.chain().focus().toggleItalic().run()}
              isActive={editor.isActive("italic")}
              icon={<FaItalic />}
              label="Italic"
            />

            <ToolbarButton
              onClick={() => editor.chain().focus().toggleUnderline().run()}
              isActive={editor.isActive("underline")}
              icon={<FaUnderline />}
              label="Underline"
            />
            <div className="divider"></div>

            <ToolbarButton
              onClick={() => editor.chain().focus().toggleBulletList().run()}
              isActive={editor.isActive("bulletList")}
              icon={<FaListUl />}
              label="Bullet List"
            />
            <ToolbarButton
              onClick={() => editor.chain().focus().toggleOrderedList().run()}
              isActive={editor.isActive("orderedList")}
              icon={<FaListOl />}
              label="Ordered List"
            />
            <div className="divider"></div>

            <div style={{ position: "relative" }}>
              <button
                type="button"
                className={`toolbar-btn ${
                  showTableSelector ? "is-active" : ""
                }`}
                onMouseDown={(e) => {
                  e.preventDefault();
                  setShowTableSelector(!showTableSelector);
                }}
              >
                <FaTable />
              </button>
              {showTableSelector && <TableSelector onSelect={insertTable} />}
            </div>

            <ToolbarButton
              onClick={handleFileClick}
              icon={<FaPaperclip />}
              label="Attach"
            />
            <div className="divider"></div>
            <ToolbarButton
              onClick={() => editor.chain().focus().undo().run()}
              icon={<FaUndo />}
              label="Undo"
            />
            <ToolbarButton
              onClick={() => editor.chain().focus().redo().run()}
              icon={<FaRedo />}
              label="Redo"
            />
          </>
        )}
        {isUploading && (
          <span style={{ marginLeft: "10px", fontSize: "12px", color: "blue" }}>
            Uploading...
          </span>
        )}
      </div>

      {isPreviewMode ? (
        <div
          className="preview-mode-container"
          dangerouslySetInnerHTML={{ __html: editor.getHTML() }}
        />
      ) : (
        <div className="editor-content-container">
          {floatingMenu.show && (
            <div
              className="floating-plus-wrapper"
              style={{ top: floatingMenu.y }}
            >
              <button
                type="button"
                className={`plus-btn ${floatingMenu.isOpen ? "open" : ""}`}
                onMouseDown={(e) => {
                  e.preventDefault();
                  e.stopPropagation();
                  setFloatingMenu((p) => ({ ...p, isOpen: !p.isOpen }));
                }}
              >
                <FaPlus />
              </button>
              {floatingMenu.isOpen && (
                <div className="plus-dropdown">
                  <button
                    type="button"
                    onMouseDown={(e) => {
                      e.preventDefault();
                      e.stopPropagation();
                      handleFileClick();
                    }}
                  >
                    <FaImage /> Image
                  </button>
                  <button
                    type="button"
                    onMouseDown={(e) => {
                      e.preventDefault();
                      e.stopPropagation();
                      handleFileClick();
                    }}
                  >
                    <FaFileAlt /> File
                  </button>
                </div>
              )}
            </div>
          )}
          <EditorContent editor={editor} />
        </div>
      )}
      <input
        type="file"
        ref={fileInputRef}
        style={{ display: "none" }}
        multiple
        onChange={handleFileChange}
      />
    </div>
  );
};

export default AdvancedEditor;
