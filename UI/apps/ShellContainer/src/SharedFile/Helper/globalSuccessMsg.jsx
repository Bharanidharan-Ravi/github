import React,{ useEffect, useRef } from "react";
import { toast } from "react-toastify";
import { useCustomStore } from "shared-store";

const GlobalSuccess = () => {
    const { globalSuccess, clearGlobalSuccess } = useCustomStore();
    const shownRef = useRef(new Set());
  
    useEffect(() => {
      if (globalSuccess && !shownRef.current.has(globalSuccess)) {
        toast.success(globalSuccess);
        shownRef.current.add(globalSuccess);
  
        setTimeout(() => {
          shownRef.current.delete(globalSuccess);
          clearGlobalSuccess
        }, 3000);
      }
    }, [globalSuccess]);
  
    return null;
  };
  
  export default GlobalSuccess;
  