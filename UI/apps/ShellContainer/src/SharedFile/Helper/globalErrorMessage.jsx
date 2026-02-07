import React,{ useEffect, useRef } from "react";
import { toast } from "react-toastify";
import { useCustomStore } from "shared-store";

const GlobalError = () => {
    const { globalError, clearGlobalError } =useCustomStore();
    const shownErrorsRef = useRef(new Set());
    
    useEffect(() => {
        if (globalError && !shownErrorsRef.current.has(globalError)) {
          toast.error(globalError);
          shownErrorsRef.current.add(globalError);
  
          setTimeout(() => {
            shownErrorsRef.current.delete(globalError);
            clearGlobalError();
          }, 5000);
        }
    }, [globalError]);
  
    return null;
  };
  
  export default GlobalError;