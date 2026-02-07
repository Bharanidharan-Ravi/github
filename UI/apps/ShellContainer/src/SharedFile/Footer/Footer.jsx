import React from 'react';
import './Footer.css'; 
const Footer = () => {
  return (
    <footer className="wg-footer">
        <span>© {new Date().getFullYear()} WorkGlow Solutions</span>
    </footer>
  );
};

export default Footer;
