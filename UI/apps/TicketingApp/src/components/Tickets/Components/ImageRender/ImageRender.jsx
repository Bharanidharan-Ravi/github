import "./ImageRender.css";

const ImageRender = ({attachment, onImageClick }) => {
    if(!attachment || attachment.length === 0) return null;

    return (
        <div
        className="image-render-grid"
            style={{
                display: "flex",
                flexWrap: "wrap",
                gap: "10px",
                // marginBottom: "10px",
            }}
        >
            {attachment?.map((image, index) => (
                <div
                className="image-render-item"
                    key={index}
                    style={{
                        position: "relative",
                        maxWidth: "75px",
                        maxHeight: "75px",
                    }}
                    onClick={() => onImageClick && onImageClick(index)}
                >
                    <img
                        src={image.publicUrl}
                        alt={`Preview ${index}`}
                        style={{
                            width: "100%",
                            height: "100%",
                            objectFit: "contain",
                            borderRadius: "4px",
                            border: "1px solid #ddd",
                            transition: "all 0.3s ease-in-out",
                        }}
                        title={image.fileName}
                    />
                </div>
            ))}
        </div>
    );
};

export default ImageRender;