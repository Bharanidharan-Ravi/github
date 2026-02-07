import TitleIcon from '@mui/icons-material/Title';
import FormatBoldIcon from '@mui/icons-material/FormatBold';
import FormatItalicIcon from '@mui/icons-material/FormatItalic';
import CodeIcon from '@mui/icons-material/Code';
import LinkIcon from '@mui/icons-material/Link';
import FormatListBulletedIcon from '@mui/icons-material/FormatListBulleted';
import FormatListNumberedIcon from '@mui/icons-material/FormatListNumbered';
import CheckBoxIcon from '@mui/icons-material/CheckBox';
import AlternateEmailIcon from '@mui/icons-material/AlternateEmail';

export const buttons = [
    { title: 'Title', icon: <TitleIcon fontSize="small" />, before: '# ', after: '' },
    { title: 'Bold', icon: <FormatBoldIcon fontSize="small" />, before: '**', after: '**' },
    { title: 'Italic', icon: <FormatItalicIcon fontSize="small" />, before: '*', after: '*' },
    // { title: 'Code', icon: <CodeIcon fontSize="small" />, before: '`', after: '`' },
    // { title: 'Link', icon: <LinkIcon fontSize="small" />, before: '[', after: '](url)' },
    { title: 'Bullet List', icon: <FormatListBulletedIcon fontSize="small" />, before: '- ', after: '' },
    { title: 'Numbered List', icon: <FormatListNumberedIcon fontSize="small" />, before: '1. ', after: '' },
    // { title: 'Checkbox', icon: <CheckBoxIcon fontSize="small" />, before: '- [ ] ', after: '' },
    { title: 'Mention', icon: <AlternateEmailIcon fontSize="small" />, before: '@', after: '' },
];

// Insert formatted text at the cursor position in a textarea
export const insertAtCursor = (textAreaRef, before, after = '', setNewComment) => {
    const textArea = textAreaRef.current;
    const start = textArea.selectionStart;
    const end = textArea.selectionEnd;
    const text = textArea.value;
    const selectedText = text.slice(start, end);
    const newText = text.slice(0, start) + before + selectedText + after + text.slice(end);
    setNewComment(newText);
    setTimeout(() => {
        const cursorPos = start + before.length + selectedText.length + after.length;
        textArea.setSelectionRange(cursorPos, cursorPos);
        textArea.focus();
    }, 0);
};

// Read a file as Base64
export const readFileAsBase64 = (file) => {
    return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onloadend = () => {
            const base64String = reader.result.split(',')[1];
            resolve(base64String);
        };
        reader.onerror = reject;
        reader.readAsDataURL(file);
    });
};

// Get file type based on file extension
export const getFileType = (file) => {
    const ext = file.name.split('.').pop().toLowerCase();
    if (['png', 'jpg', 'jpeg', 'gif', 'bmp'].includes(ext)) return 'image';
    if (['pdf'].includes(ext)) return 'pdf';
    if (['xls', 'xlsx', 'csv'].includes(ext)) return 'excel';
    if (['doc', 'docx'].includes(ext)) return 'doc';
    return 'other';
};

// -----------------------------------------------------------------------------------------------------

const filterData = (data = [], searchTerm = "", keys = []) => {
    if (!searchTerm.trim()) return data;

    const lowerTerm = searchTerm.toLowerCase();

    return data.filter((item) => {
        const fieldsToSearch = keys.length > 0 ? keys : Object.keys(item);

        return fieldsToSearch.some((key) => {
            const value = item[key];

            if (Array.isArray(value)) {
                return value.some((v) =>
                    String(v).toLowerCase().includes(lowerTerm)
                );
            }

            if (typeof value === "string" || typeof value === "number") {
                return String(value).toLowerCase().includes(lowerTerm);
            }

            if (typeof value === "object" && value !== null) {
                return Object.values(value).some((v) =>
                    String(v).toLowerCase().includes(lowerTerm)
                );
            }
            return false;
        });
    });
};

export default filterData;