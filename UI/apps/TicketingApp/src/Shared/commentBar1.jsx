import React, { useState } from "react";
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faTimes } from '@fortawesome/free-solid-svg-icons';
import { postImage, RemoveImage } from 'shared-store';
import { useTicketStore } from "../components/Tickets/TicketStore/TicketStore";

function CommentBar1() {
    const [text, setText] = useState(""); // Stores the text content
    const [dragging, setDragging] = useState(false); // Tracks if the user is dragging something over the area
    const { addImages, AttachImages, removeImage, 
        handleInputChange,formData } = useTicketStore();
    // Allowed file types (both image and document types)
    const allowedFileTypes = [
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/jpg",
        "application/pdf",
        "application/msword", // docx
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document", // docx
        "application/vnd.ms-excel", // Excel 97-2003
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", // Excel xlsx
    ];


    // Maximum number of images
    const MAX_IMAGES = 10;
    // Maximum file size 10MB (10 * 1024 * 1024 bytes)
    const MAX_FILE_SIZE = 10 * 1024 * 1024;

    // Handle the drag over event (allow drop)
    const handleDragOver = (e) => {
        e.preventDefault();
        setDragging(true);
    };

    // Handle the drag leave event
    const handleDragLeave = (e) => {
        e.preventDefault();
        setDragging(false);
    };

    // Handle the drop event (process image or text)
    const handleDrop = async (e) => {
        e.preventDefault();
        setDragging(false);

        const droppedFiles = e.dataTransfer.files;
        const droppedText = e.dataTransfer.getData("text/plain");

        // Handle image files dropped

        // Handle image and document files dropped
        if (droppedFiles.length > 0) {
            // Check if number of images is already at the limit
            if (AttachImages.length >= MAX_IMAGES) {
                alert(`You can only upload up to ${MAX_IMAGES} files.`);
                return;
            }

            // Check for duplicate files by comparing the file name and type
            const newFiles = Array.from(droppedFiles).filter((file) => {
                // Check if the file type is allowed
                if (!allowedFileTypes.includes(file.type)) {
                    alert("Invalid file type! Only images and documents are allowed.");
                    return false;
                }

                // Check if the file size exceeds the limit
                if (file.size > MAX_FILE_SIZE) {
                    alert("File is too large! Maximum size is 10MB.");
                    return false;
                }
                // Check if the file is already in the images array (by file name or URL)
                return !AttachImages.some((img) => img.name === file.name);
            });

            if (newFiles.length === 0) {
                alert("This file has already been added.");
                return;
            }
            for (const file of newFiles) {
                try {
                    const fileUrl = await uploadFileToTempAsync(file); // Upload the file
                    // const imageUrl = URL.createObjectURL(fileUrl);
                    console.log("fileUrl :", { fileUrl });

                    const newFile = {
                        url: fileUrl.publicUrl,
                        name: fileUrl.fileName, // Store the file name for hover tooltip
                        type: file.type,
                        LocalPath: fileUrl.localPath
                    };
                    addImages(newFile);
                } catch (error) {
                    console.log();

                    alert(`Error uploading file: ${error.message}`);
                }
            }
        } else if (droppedText) {
            // Append text if text is dropped
            console.log("text trigger");
            
            handleInputChange("description", droppedText);
            setText((prevText) => prevText + droppedText);
        }
    };

    // Function to upload file to the server
    const uploadFileToTempAsync = async (file) => {
        try {
            const response = await postImage(file);
            // console.log("response :", response);
            // const data = await response.json();
            // console.log("data :",data);
            return response; // Assuming the API returns the file path or URL
        } catch (error) {
            throw new Error(`File upload failed: ${error.message}`);
        }
    };
    const handleDeleteClick = (e, image) => {
        e.preventDefault();     // stops form submit
        e.stopPropagation();    // stops bubbling
        removeImageApi(image);
    };
    // Remove image by filtering out the image by index
    const removeImageApi = async (image) => {
        console.log("imageUrl :", image);
        try {
            // Prepare payload exactly as backend expects
            const Imagepayload = {
                delete: "single",
                temps: [
                    {
                        fileName: image.name,
                        publicUrl: image.url,
                        LocalPath: image.LocalPath   // Must match backend LocalPath
                    }
                ]
            };

            console.log("Delete payload:", Imagepayload);

            await RemoveImage(Imagepayload);  // API call
            removeImage(image.url);
          
        } catch (err) {
            console.error("Delete failed:", err);
            alert("Failed to delete file: " + err.message);
        }
    };

    return (
        <div className="App">
            {/* Main Container for Text and Drag-and-Drop */}
            <div
                onDragOver={handleDragOver}
                onDragLeave={handleDragLeave}
                onDrop={handleDrop}
                style={{
                    border: "1px solid #ccc",
                    padding: "10px",
                    width: "100%",
                    // maxWidth: "600px",
                    margin: "0",
                    borderRadius: "4px",
                    backgroundColor: "#f9f9f9",
                    minHeight: "200px",
                    position: "relative",
                    overflowY: "auto",
                }}
            >
                {/* Display Image Previews */}
                {AttachImages.length > 0 && (
                    <div
                        style={{
                            display: "flex",
                            flexWrap: "wrap",
                            gap: "10px",
                            marginBottom: "10px",
                        }}
                    >
                        {AttachImages && AttachImages.map((image, index) => (
                            <div
                                key={index}
                                style={{
                                    position: "relative",
                                    maxWidth: "75px",
                                    maxHeight: "75px",
                                }}
                            >
                                <img
                                    src={image.url}
                                    alt={`Preview ${index}`}
                                    style={{
                                        width: "100%",
                                        height: "100%",
                                        objectFit: "contain", // Ensure the image fits within the container
                                        borderRadius: "4px",
                                        border: "1px solid #ddd",
                                        transition: "all 0.3s ease-in-out",
                                    }}
                                    title={image.name} // Display the image name on hover
                                />
                                {/* Close Icon to remove the image */}
                                <button
                                    onClick={(e) => handleDeleteClick(e, image)}
                                    style={{
                                        position: 'absolute',
                                        top: '-5px', // Adjust to move the button closer to the corner
                                        right: '-5px', // Adjust to move the button closer to the corner
                                        background: 'rgba(0,0,0,0.6)',
                                        color: 'white',
                                        border: 'none',
                                        borderRadius: '50%',
                                        padding: '2px', // Smaller padding
                                        cursor: 'pointer',
                                        fontSize: '10px', // Smaller font size for icon
                                        width: '14px', // Smaller button size
                                        height: '14px', // Smaller button size
                                        display: 'flex',
                                        justifyContent: 'center',
                                        alignItems: 'center',
                                    }}
                                >
                                    <FontAwesomeIcon icon={faTimes} size="xs" /> {/* FontAwesome close icon */}
                                </button>
                            </div>
                        ))}
                    </div>
                )}

                {/* Text Area Section */}
                <textarea
                    value={formData.description}
                    onChange={(e) => handleInputChange("description",e.target.value)}
                    placeholder="Write a comment here..."
                    rows="6"
                    style={{
                        width: "100%",
                        padding: "10px",
                        marginTop: "10px", // Push down if images exist
                        borderRadius: "4px",
                        border: "1px solid #ccc",
                        fontSize: "16px",
                        minHeight: "100px",
                        resize: "none",
                        overflow: "auto",
                    }}
                />

                {/* Drag-and-Drop Text Placeholder */}
                {!addImages.length && !text && !dragging && (
                    <div
                        style={{
                            textAlign: "center",
                            color: "#888",
                            position: "absolute",
                            top: "50%",
                            left: "50%",
                            transform: "translate(-50%, -50%)",
                            fontSize: "14px",
                        }}
                    >
                        Drag images or text here
                    </div>
                )}
            </div>
        </div>
    );
}

export default CommentBar1;
