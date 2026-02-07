import React, { useRef } from 'react';
import { Box, Avatar, Typography, Paper, Tabs, Tab, Tooltip, IconButton, Button } from '@mui/material';
import AttachFileIcon from '@mui/icons-material/AttachFile';
import DeleteIcon from '@mui/icons-material/Delete';
import CreateIcon from '@mui/icons-material/Create'; 
import VisibilityIcon from '@mui/icons-material/Visibility'; 
import ReactMarkdown from 'react-markdown';
import { buttons, getFileType, insertAtCursor, readFileAsBase64 } from '../../Shared/Utilities/Utilities';
import './FormBuilder.css';

const FormBuilder = ({
  tab,
  setTab,
  commentContent,
  setCommentContent,
  onAddComment
}) => {
  const textAreaRef = useRef(null);

  const onFileChange = async (e) => {
    const files = Array.from(e.target.files || []);
    if (!files.length) return;
  
    try {
      const imagedata = await Promise.all(
        files.map(async (file) => {
          const base64 = await readFileAsBase64(file);
          const fileType = getFileType(file);
          return { name: file.name, type: fileType, base64 };
        })
      );
  
      const fileNames = imagedata.map((img) => img.name).join(', ');
  
      setCommentContent((prev) => ({
        ...prev,
        images: [...(prev.images || []), ...imagedata],
        text: `${prev.text}${prev.text ? '\n' : ''}${fileNames}`, 
      }));
    } catch (err) {
      console.error('File read error:', err);
    }
  };

  const removeImage = (index) => {
    setCommentContent((prev) => {
      const updatedImages = prev.images.filter((_, i) => i !== index);
      return { ...prev, images: updatedImages };
    });
  };

  return (
    <Box className="form-builder">
      <Box className="comment-header">
        <Typography className="comment-title">Add a comment</Typography>
      </Box>

      <Paper className="comment-paper">
        <Box className="tabs-toolbar-container">
          <Tabs
            value={tab}
            onChange={(e, val) => setTab(val)}
            variant="scrollable"
            scrollButtons="auto"
            className="comment-tabs"
          >
            {/* <Tab label="Write" />
            <Tab label="Preview" /> */}
            <Tab icon={<CreateIcon fontSize="small" />} /> 
            <Tab icon={<VisibilityIcon fontSize="small" />} /> 
          </Tabs>

          {tab === 0 && (
            <>
              {buttons.map(({ title, icon, before, after }, idx) => (
                <Tooltip key={idx} title={title}>
                  <IconButton
                    size="small"
                    onClick={() =>
                      insertAtCursor(textAreaRef, before, after, (newVal) =>
                        setCommentContent((prev) => ({ ...prev, text: newVal }))
                      )
                    }
                    className="toolbar-button"
                  >
                    {icon}
                  </IconButton>
                </Tooltip>
              ))}
            </>
          )}
        </Box>

        {tab === 0 ? (
          <Box
            component="textarea"
            ref={textAreaRef}
            placeholder="Leave a comment"
            rows={6}
            className="comment-textarea"
            value={commentContent.text}
            onChange={(e) =>
              setCommentContent((prev) => ({ ...prev, text: e.target.value }))
            }
          />
        ) : (
            <Box className="comment-preview">
            <ReactMarkdown>{commentContent.text || '*No preview yet*'}</ReactMarkdown>
          
            {commentContent.images?.length > 0 && (
              <Box className="preview-files">
                {commentContent.images.map((img, i) => (
                  <Box key={i} className="file-preview-item">
                    {img.type === 'image' ? (
                      <Box className="image-preview-item">
                        <img
                          src={`data:image/*;base64,${img.base64}`}
                          alt={img.name}
                          className="preview-image"
                        />
                      </Box>
                    ) : (
                      <Box className="file-preview">
                        <AttachFileIcon fontSize="small" /> {img.name}
                      </Box>
                    )}
                    
                    <IconButton
                      size="small"
                      onClick={() => removeImage(i)}
                      className="remove-file-button"
                    >
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </Box>
                ))}
              </Box>
            )}
          </Box>
          
        )}

        <Box className="bottom-controls">
          <Box className="attachment-label">
            <input
              accept="*/*"
              style={{ display: 'none' }}
              id="attachment-upload"
              type="file"
              multiple
              onChange={onFileChange}
            />
            <label htmlFor="attachment-upload">
              <IconButton component="span" size="small" className="file-attachment-button">
                <AttachFileIcon fontSize="small" />
              </IconButton>
            </label>
            <Typography className="attachment-text">Attachments</Typography>
          </Box>

          <Button
            variant="contained"
            onClick={onAddComment}
            disabled={!commentContent.text.trim()}
            className="comment-button"
          >
            Comment
          </Button>
        </Box>
      </Paper>
    </Box>
  );
};

export default FormBuilder;
