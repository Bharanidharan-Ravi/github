import { useEffect } from "react";
import {
    useLocation,
    useNavigate
} from "react-router-dom";

export function useSkipHistory(skipTo) {
    const navigate = useNavigate();
    const location = useLocation();    

    useEffect(() => {
        const last = sessionStorage.getItem("skip-history");
        if (last === location.pathname) {
            sessionStorage.removeItem("skip-history");
            navigate(skipTo, {replace: true});
        }
    }, [location.pathname, navigate]);  
}

export function markSkipHistory(path) {
    sessionStorage.setItem("skip-history", path);
}