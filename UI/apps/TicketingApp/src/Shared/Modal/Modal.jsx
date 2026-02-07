import React, { useState, useEffect } from 'react';
import './Modal.css';

const Modal = ({ isOpen, onClose, children, title }) => {
  const [showModal, setShowModal] = useState(isOpen);
  const [animateClass, setAnimateClass] = useState('');

  useEffect(() => {
    if (isOpen) {
      setShowModal(true);
      setAnimateClass('modal-open');
      document.body.style.overflow = 'hidden'; 
    } else if (showModal) {
      setAnimateClass('modal-close');
      const timeout = setTimeout(() => {
        setShowModal(false);
        document.body.style.overflow = ''; 
      }, 300);

      return () => clearTimeout(timeout);
    }

    return () => {
      document.body.style.overflow = '';
    };
  }, [isOpen]);

  if (!showModal) return null;

  return (
    <div className={`custom-modal-overlay ${animateClass}`} onClick={onClose}>
      <div
        className={`custom-modal-content ${animateClass}`}
        onClick={(e) => e.stopPropagation()}
      >
        <div className="custom-modal-header">
          <h4>{title}</h4>
          <button className="custom-modal-close" onClick={onClose}>×</button>
        </div>
        {children}
      </div>
    </div>
  );
};

export default Modal;