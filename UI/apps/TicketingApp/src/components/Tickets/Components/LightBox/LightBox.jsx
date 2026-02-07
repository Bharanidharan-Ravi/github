import { useEffect, useState } from "react";
import "./LightBox.css";
const Lightbox = ({ attachment, lightboxIndex, setLightboxIndex, onclose }) => {
    const total = attachment.length;

    useEffect(() => {
        const handleKey = (e) => {
            if (e.key === "Escape") {
                onclose();
            } else if (e.key === "ArrowLeft") {
                setLightboxIndex((perv) => (perv === 0 ? total - 1 : perv - 1));
            } else if (e.key === "ArrowRight") {
                setLightboxIndex((perv) => (perv === total - 1 ? 0 : perv + 1));
            }
        };
        window.addEventListener("keydown", handleKey);
        return () => { window.removeEventListener("keydown", handleKey); };
    }, [onclose, setLightboxIndex, total]);

    if (!attachment || total === 0) return null;

    const current = attachment[lightboxIndex];

    return (
        <div
            className="lightbox"
            onClick={onclose}
        >
            {/* Left Arrow */}
            {total > 1 && (
                <span
                    className="lightbox-arrow left"
                    onClick={(e) => {
                        e.stopPropagation();
                        setLightboxIndex((prev) =>
                            prev === 0 ? total - 1 : prev - 1
                        );
                    }}>
                    ❮
                </span>
            )}

            {/* Image */}
            <img
                src={attachment[lightboxIndex].publicUrl}
                className="lightbox-img"
                onClick={(e) => e.stopPropagation()}
            />

            {/* Right Arrow */}
            {total > 1 && (
                <span
                    className="lightbox-arrow right"
                    onClick={(e) => {
                        e.stopPropagation();
                        setLightboxIndex((prev) =>
                            prev === total - 1 ? 0 : prev + 1
                        );
                    }}
                >
                    ❯
                </span>
            )}
        </div>
    );
}

export default Lightbox;