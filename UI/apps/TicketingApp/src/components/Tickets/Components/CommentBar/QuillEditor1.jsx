import React, { useEffect, useRef } from "react";
import Quill from "quill";
import "quill/dist/quill.snow.css";

// Toolbar Options
const TOOLBAR_OPTIONS = [
  ["bold", "italic", "underline"],
  [{ list: "ordered" }, { list: "bullet" }],
  ["clean"]
];

export default function QuillEditor1({ value, onChange, setQuillInstance }) {
  const containerRef = useRef(null);
  const quillRef = useRef(null);

  // Init Quill (run once)
  useEffect(() => {
    if (!containerRef.current || quillRef.current) return;

    const q = new Quill(containerRef.current, {
      theme: "snow",
      modules: {
        toolbar: TOOLBAR_OPTIONS
      },
    });

    quillRef.current = q;
    if (setQuillInstance) setQuillInstance(q);

    // Send HTML to parent (onChange)
    const handle = () => {
      const html = q.root.innerHTML;
      onChange(html);
    };

    q.on("text-change", handle);

    return () => {
      q.off("text-change", handle);
    };
  }, []);

  // Sync external value → Quill editor
  useEffect(() => {
    const q = quillRef.current;
    if (!q) return;

    const currentHtml = q.root.innerHTML;
    if (value != null && value !== currentHtml) {
      const delta = q.clipboard.convert(value);
      q.setContents(delta);
    }
  }, [value]);

  return (
    <div
      ref={containerRef}
      style={{
        minHeight: 150,
        borderRadius: 6,
        background: "#fff",
      }}
    />
  );
}