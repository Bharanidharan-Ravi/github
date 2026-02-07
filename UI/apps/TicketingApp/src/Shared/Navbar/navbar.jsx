import { NavLink } from 'react-router-dom';
import "./navbar.css";
import { roleRoutes } from '../../Router/routesConfig';
import { useCustomStore } from "shared-store";

const Navbar = ({ basePath, isMobile, showMobileMenu, toggleMobileMenu  }) => {
  // Build role-prefixed links dynamically
  const isStandalone = !window.__MICRO_FRONTEND__;
  const role = useCustomStore(state => state.role);
  const links = () => {
    return roleRoutes[2].map((link) => {
      // Prepend basePath to the path if standalone
      if (!isStandalone) {
        return {  
          ...link,
          path: `/${basePath}${link.path}`,
        };
      } else {
        return {
          ...link,
          path: `${link.path}`,
        };
      }
    });
  };

  return (
    <nav className={` ${isMobile ? 'mobile-nav' : 'nav'}`}>
      {!isMobile && (
        <div className="nav-links">
          {links().map(link => (
            <NavLink
              key={link.path}
              to={link.path}
              className="tab"
              activeclassname="active"
              style={({ isActive }) => ({ fontWeight: isActive ? 'bold' : 'normal' })}
            >
              {link.name}
            </NavLink>
          ))}
        </div>
      )}

      {/* Mobile Menu */}
      {isMobile && showMobileMenu && (
        <div className="mobile-menu">
          {links().map((link) => (
            <NavLink
            key={link.path}
            to={link.path}
            className="tab"
            activeclassname="active"
            style={({ isActive }) => ({ fontWeight: isActive ? 'bold' : 'normal' })}
          >
            {link.name}
          </NavLink>
          ))}
        </div>
      )}
    </nav>
  );
}

//   return (
//     <nav className="nav">
//       <div className="nav-links">
//         {links().map((link) => (
//           <NavLink
//             key={link.name}
//             to={link.path}
//             className={({ isActive }) =>
//               `tab ${isActive ? "active" : ""}`
//             }
//           >
//             {link.name}
//           </NavLink>
//         ))}
//       </div>
//     </nav>
//   );
// };

export default Navbar;


// const Navbar = ({ role }) => {
//   const links = roleRoutes;
//   console.log("links :", links);


//   return (
//     <nav className="nav">
//       <div className="nav-links">
//         {links.map(link => (
//           <NavLink
//             key={link.path}
//             to={link.path}
//             className="tab"
//             activeclassname="active"
//             style={({ isActive }) => ({ fontWeight: isActive ? 'bold' : 'normal' })}
//           >
//             {link.name}
//           </NavLink>
//         ))}
//       </div>
//     </nav>
//   );
// };

// export default Navbar;


// import { NavLink } from 'react-router-dom';
// import './Navbar.css';
// // import { getBaseUrl } from '../../Shared/detectPrefix';
// import { FaTachometerAlt, FaTicketAlt, FaProjectDiagram } from 'react-icons/fa';

// export default function Navbar({ isMobile, showMobileMenu, toggleMobileMenu }) {
//     // const baseUrl = getBaseUrl();

//     const links = [
//         { name: 'Nest', path: 'dashboard', icon: <FaTachometerAlt /> },
//         { name: 'Tickets', path: 'tickets', icon: <FaTicketAlt /> },
//         { name: 'Projects', path: 'projects', icon: <FaProjectDiagram /> },
//         { name: 'Repository', path: '/repository', icon: '📚' },
//     ];

//     return (
//         <nav className="nav">
//             {/* lg screens */}
//             {!isMobile && (
//                 <div className="nav-links">
//                     {links.map((link) => (
//                         <NavLink
//                             key={link.path}
//                             to={`${link.path}`}
//                             className="tab"
//                             activeclassname="active"
//                             style={({ isActive }) => ({ fontWeight: isActive ? 'normal' : '600' })}
//                         >
//                             {link.icon}
//                             {link.name}
//                         </NavLink>
//                     ))}
//                 </div>
//             )}

//             {/* Mobile*/}
//             {isMobile && (
//                 <>
//                     {showMobileMenu &&  (
//                         <div className="mobile-menu">
//                             {links.map((link) => (
//                                 <NavLink
//                                     key={link.path}
//                                     to={`${baseUrl}/${link.path}`}
//                                     className="tab"
//                                     activeclassname="active"
//                                     style={({ isActive }) => ({ fontWeight: isActive ? 'normal' : '300' })}
//                                     onClick={() => toggleMobileMenu(false)}
//                                 >
//                                     {link.name}
//                                 </NavLink>
//                             ))}
//                         </div>
//                     )}
//                 </>
//             )}
//         </nav>
//     );
// }
