import React, { useState } from "react";
import { NavLink } from "react-router-dom";
import * as Icons from "lucide-react";

const getIcon = (name) => {
  const Icon = Icons[name] || Icons.Circle;
  return <Icon size={18} />;
};

const Sidebar = ({ items = [], isOpen, onClose }) => {
  const [expanded, setExpanded] = useState({});
console.log("item :", items);

  const toggle = (key) =>
    setExpanded((p) => ({ ...p, [key]: !p[key] }));

  return (
    <>
      {isOpen && (
        <div
          className="position-fixed top-0 start-0 w-100 h-100 bg-dark opacity-50 d-md-none"
          onClick={onClose}
        />
      )}

      <aside
        className={`bg-dark text-white p-3
        ${isOpen ? "fixed-top h-100" : "d-none d-md-block"}`}
        style={{ width: 260 }}
      >
        <ul className="nav flex-column">
          {items.map((item) => (
            <li key={item.key} className="nav-item mb-1">

              {item.children ? (
                <>
                  <div
                    className="nav-link d-flex justify-content-between"
                    style={{ cursor: "pointer" }}
                    onClick={() => toggle(item.key)}
                  >
                    <span>{getIcon(item.icon)} {item.label}</span>
                    <Icons.ChevronDown
                      size={16}
                      style={{
                        transform: expanded[item.key]
                          ? "rotate(180deg)"
                          : "rotate(0deg)",
                      }}
                    />
                  </div>

                  {expanded[item.key] && (
                    <ul className="nav flex-column ms-3">
                      {item.children.map((child) => (
                        <li key={child.key}>
                          <NavLink
                            to={child.path}
                            className="nav-link small"
                            onClick={onClose}
                          >
                            {child.label}
                          </NavLink>
                        </li>
                      ))}
                    </ul>
                  )}
                </>
              ) : (
                <NavLink to={item.path} className="nav-link">
                  {getIcon(item.icon)} {item.label}
                </NavLink>
              )}

            </li>
          ))}
        </ul>
      </aside>
    </>
  );
};

export default Sidebar;
