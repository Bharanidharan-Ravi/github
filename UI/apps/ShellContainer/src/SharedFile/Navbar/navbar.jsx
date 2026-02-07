import { NavLink } from 'react-router-dom';
import { roleRoutes } from '../../routes/routesConfig';
import { FaTachometerAlt, FaTicketAlt, FaProjectDiagram } from 'react-icons/fa';

export default function Navbar({ role, isMobile, showMobileMenu, toggleMobileMenu }) {
  const links = roleRoutes[role] || [];
  return (
    <nav className={`nav ${isMobile ? 'mobile-nav' : ''}`}>
      {!isMobile && (
        <div className="nav-links">
          {links.map(link => (
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
          {links.map((link) => (
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

// const Navbar = ({ role }) => {
//   const links = roleRoutes[role] || [];
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

