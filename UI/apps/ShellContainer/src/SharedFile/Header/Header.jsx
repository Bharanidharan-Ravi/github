// import React from 'react';
// import { IoMenu } from 'react-icons/io5';

// const Header = ({ toggleMobileMenu }) => {
//   return (
//     <header className="bg-light py-3">
//       <div className="container">
//         <div className="d-flex justify-content-between align-items-center">
//           <button className="menu-toggle d-block d-lg-none" onClick={toggleMobileMenu}>
//             <IoMenu size={24} />
//           </button>
//           <div className="d-flex justify-content-center flex-grow-1 d-lg-flex justify-content-lg-start">
//             <img
//               src="/WORKGLOW LOGO.png"
//               alt="Logo"
//               className="d-inline-block align-top"
//               style={{ width: '120px' }}
//             />
//           </div>
//         </div>
//       </div>
//     </header>
//   );
// };

// export default Header;

import React, { useState } from 'react';
import { IoMenu } from 'react-icons/io5';
import { useNavigate } from 'react-router-dom';
import "./Header.css";

const Header = ({ toggleMobileMenu }) => {
  const navigate = useNavigate();

  const UserName = 'Test'; 
  const [dropdownVisible, setDropdownVisible] = useState(false);

  const handleIconClick = () => {
    setDropdownVisible(prev => !prev);
  };

  const handleLogout = () => {
    localStorage.removeItem('user'); 
    navigate('/');
  };

  const firstLetter = UserName.charAt(0).toUpperCase();

  return (
    <header className="header py-3">
      <div className="container">
        <div className="d-flex justify-content-between align-items-center">
          <button className="menu-toggle d-block d-lg-none" onClick={toggleMobileMenu}>
            <IoMenu size={24} />
          </button>

          <div className="d-flex justify-content-center flex-grow-1 d-lg-flex justify-content-lg-start">
            <img
              src="/WORKGLOW LOGO.png"
              alt="Logo"
              className="d-inline-block align-top"
              style={{ width: '120px' }}
            />
          </div>

          <div className="dropdown">
            <div className="userheader d-flex align-items-center" onClick={handleIconClick}>
              <div className="avatar-circle d-flex justify-content-center align-items-center">
                {firstLetter}
              </div>
            </div>

            {dropdownVisible && (
              <div className="dropdown-menu-custom shadow-sm p-3 mt-2  border">
                <div className="text-center">
                  <strong>{UserName}</strong>
                </div>
                <button
                  className="logout-btn mt-2 text-center rounded"
                  onClick={handleLogout}
                >
                  Logout
                </button>
              </div>
            )}
          </div>
        </div>
      </div>
    </header>
  );
};

export default Header;
