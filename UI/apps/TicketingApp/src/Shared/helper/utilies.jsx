import { useCustomStore } from "shared-store";
import DOMPurify from "dompurify";

export function ProjectFilter(projId) {
    const { Project } = useCustomStore.getState();
    console.log("Project :", Project);
}

export function HtmlRenderer({ html }) {
    const cleanHtml = DOMPurify.sanitize(html);

    return (
        <div
            className="html-renderer"
            dangerouslySetInnerHTML={{__html: cleanHtml}}
        />
    );
};