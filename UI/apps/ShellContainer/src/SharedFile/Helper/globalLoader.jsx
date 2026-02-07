import React from "react";
import { useCustomStore } from "shared-store";
import SwirlLoader from "../Loader/SwirlLoader";

const GlobalLoader = () => {
    const loadingCount = useCustomStore((state) => state.globalLoading);
   
    if (loadingCount === 0) return null;
  
    return (
      <div className="loader loaderbg position-fixed top-0 start-0 w-100 h-100 d-flex justify-content-center align-items-center" style={{ zIndex: 5000 }}>
        <SwirlLoader />
      </div>
    );
  };
  
  export default GlobalLoader;
  