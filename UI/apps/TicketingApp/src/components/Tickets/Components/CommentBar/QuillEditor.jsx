import React, { useEffect, useRef } from "react";
import Quill from "quill";
import "quill/dist/quill.snow.css";

// Toolbar Options
const TOOLBAR_OPTIONS = [
    ["bold", "italic", "underline"],
    [{ list: "ordered" }, { list: "bullet" }],
    ["link"],
    ["clean"]
];

function getQuillIndex(quill, x, y) {
    const root = quill.root;
    const doc = document;
    let range = null;

    if (doc.caretRangeFromPoint) {
        range = doc.caretRangeFromPoint(x, y);
    } else if (doc.caretPositionFromPoint) {
        const pos = doc.caretPositionFromPoint(x, y);
        if (pos) {
            range = doc.createRange();
            range.setStart(pos.offsetNode, pos.offset);
        }
    }

    if (!range || !root.contains(range.startContainer)) {
        return quill.getLength();
    }

    let container = range.startContainer;
    let offset = range.startOffset;

    if (container === root) {
        container = root.childNodes[Math.min(offset, root.childNodes.length - 1)];
        offset = 0;
    }

    const blot = Quill.find(container, true);
    return blot ? blot.offset(quill.scroll) + offset : quill.getLength();
}

function getEditorImages(quill) {
    const data = quill.getContents();
    const images = [];
    delta.ops.forEach((op) => {
        if (op.insert && op.insert.image) {
            images.push(op.insert.images);
        }
    });
    return images;
}

export default function QuillEditor({ value, onChange, setQuillInstance }) {
    const containerRef = useRef(null);
    const quillRef = useRef(null);
    const currentImagesRef = useRef([]);
    const dragTracker = useRef({ isDragging: false, x: 0, y: 0 });

    // Init Quill (run once)
    useEffect(() => {
        // 1.DOM RESET
        if (containerRef.current) { containerRef.current.innerHTML = ""; }

        // 2. CREATE EDITOR DIV
        const editorDiv = document.createElement("div");
        containerRef.current.appendChild(editorDiv);

        // 3.INITIALIZE QUILL
        const quill = new Quill(containerRef.current, {
            theme: "snow",
            modules: {
                toolbar: TOOLBAR_OPTIONS
            },
        });

        quillRef.current = quill;
        // Initilize image list
        currentImagesRef.current = getEditorImages(quill);

        // --- CURSOR SETUP ---
        const cursor = document.createElement("div");
        Object.assign(cursor.style, {
            position: "absolute",
            display: "none",
            backgroundColor: "black",
            width: "2px",
            zIndex: "1000",
            pointerEvents: "none",
        });
        quill.container.style.position = "relative";
        quill.container.appendChild(cursor);

        if (onEditorReady) {
            onEditorReady(containerRef.current);
        }

        // ---HANDLER: TEXT CHANGE & IMAGE DELETE DETECTION ---
        quill.on("text-change", () => {
            const html = quill.root.innerHTML;
            onChange?.(html);

            // check for Deleted images
            if (onImageRemove) {
                const newImages = getEditorImages(quill);
                const oldImages = currentImagesRef.current;

                // find images that were in oldimages but not in newImages
                const removedImages = oldImages.filter(
                    (img) => !newImages.includes(img)
                );

                removedImages.forEach((url) => {
                    console.log("image deleted :", url);
                    onImageRemove(url);
                });

                // update ref for next change
                currentImagesRef.current = newImages;
            }
        });

        // --- HELPER: UPDATE CURSOR POSITION ---
        const updateCursorVisuals = (x, y) => {
            const index = getQuillIndex(quill, x, y);

            //update internal selection
            // using silent to pervent triggering text-change events unnecessarily
            quill.setSelection(index, 0, "silent");

            //update visual cursor
            const bounds = quill.getBounds(index);

            if (bounds) {
                cursor.style.display = "block";
                cursor.style.top = `${bounds.top}px`;
                cursor.style.left = `${bounds.left}px`;
                cursor.style.height = bounds.height > 0 ? `${bounds.height}px` : "18px";
            }
        };

        // ---- DRAG & DROP HANDLERS ---

        const handleDrop = async (e) => {
            e.preventDefault();
            e.stopPropagation();
            cursor.style.display = "none";
            dragTracker.current.isDragging = false;

            const dt = e.dataTransfer;
            if (!dt || !dt.files || dt.files.length === 0) return;

            const imageFiles = Array.from(dt.files).filter((f) =>
                f.type.startsWith("image/")
            );

            if (imageFiles.length === 0) return;

            const index = getQuillIndex(quill, e.clientX, e.clientY);
            quill.focus();

            let pos = index;

            for (const f of imageFiles) {
                const url = await uploadImage(f);
                quill.insertEmbed(pos, "image", url, "user");
                pos++;
            }
            quill.setSelection(pos, 0);

            currentImagesRef.current = getEditorImages(quill);
        };

        const handleDragOver = (e) => {
            e.preventDefault();
            e.stopPropagation();

            if (e.dataTransfer.types.includes("Files")) {
                quill.focus();

                dragTracker.current.isDragging = true;
                dragTracker.current.x = e.clientX;
                dragTracker.current.y = e.clientY;

                updateCursorVisuals(e.clientX, e.clientY);
            }
        };

        const handleDragLeave = (e) => {
            // Only hide if leaving the main editor root 
            if (!quill.root.contains(e.relatedTraget)) {
                cursor.style.display = "none";
                dragTracker.current.isDragging = false;
            }
        };

        //--- FIX FOR SCROLLING ---
        // This event fires when the editor auto-scrolls during a drag

        const handleScroll = () => {
            if (dragTracker.current.isDragging) {
                // Recalculate cursor position using the last known mouse coordinates
                // this keeps the cursor correctly positioned relative to the scrolling text
                updateCursorVisuals(dragTracker.current.x, dragTracker.current.y);
            }
        };

        // attach listeners
        quill.root.addEventListener("drop", handleDrop);
        quill.root.addEventListener("dragover", handleDragOver);
        quill.root.addEventListener("dragleave", handleDragLeave);

        quill.root.addEventListener("scroll", handleScroll);

        return () => {
            quill.root.removeEventListener("drop", handleDrop);
            quill.root.removeEventListener("dragover", handleDragOver);
            quill.root.removeEventListener("dragleave", handleDragLeave);
            quill.root.removeEventListener("scroll", handleScroll);

            quillRef.current = null;
            if (containerRef.current) {
                containerRef.current.innerHTML = "";
            }
        };
    }, []);

    // Sync external value → Quill editor
    useEffect(() => {
        if (!quillRef.current) return;
        if (value !== quillRef.current.root.innerHTML) {
            if (quillRef.current.root.innerHTML !== value) {
                quillRef.current.root.innerHTML = value || "";

                currentImagesRef.current = getEditorImages(quillRef.current);
            }
        }
    }, [value]);

    return (
        <div
            ref={containerRef}
            style={{
                minHeight: 130,
            }}
        />
    );
}