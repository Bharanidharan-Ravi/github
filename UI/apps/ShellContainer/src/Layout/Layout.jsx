import React, { useEffect, useState } from 'react';
import Header from '../SharedFile/Header/Header';
import Footer from '../SharedFile/Footer/Footer';
import { Outlet } from 'react-router-dom';
import { useLocation } from 'react-router-dom';
import './Layout.css';
import Navbar from '../SharedFile/Navbar/navbar';

const Layout = ({ role }) => {
    const location = useLocation()
    const hideLayout = location.pathname === '/auth'
    const [isMobile, setIsMobile] = useState(window.innerWidth < 768);
    const [showMobileMenu, setShowMobileMenu] = useState(false);

    useEffect(() => {
        const handleResize = () => {
            setIsMobile(window.innerWidth < 768);
            if (window.innerWidth >= 768) {
                setShowMobileMenu(false);
            }
        };

        window.addEventListener('resize', handleResize);
        handleResize();

        return () => window.removeEventListener('resize', handleResize);
    }, []);

    const toggleMobileMenu = () => {
        setShowMobileMenu((prev) => !prev);
    };

    return (
        <div className="layout-container">
            {!hideLayout && <Header toggleMobileMenu={toggleMobileMenu} />}
            {/* Conditionally render content based on role */}
            <main className="main-content">
                {/* <Navbar role={role} /> */}
                <Outlet />
                {/* {role === "3" ? (
                    <Outlet />
                ) : role === "2" ? (
                    <>
                        <Navbar role={role}/>
                        <Outlet />
                    </>
                ) : (
                    <Outlet />
                )} */}
            </main>
            <Footer />
        </div>
    )
}

export default Layout

