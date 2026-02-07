import React, { Suspense, useContext, useState } from 'react'
import CentralRouter from './routes/CentralRouter'
import { ToastContainer } from 'react-toastify'
import GlobalError from './SharedFile/Helper/globalErrorMessage'
import GlobalLoader from './SharedFile/Helper/globalLoader'
import GlobalSuccess from './SharedFile/Helper/globalSuccessMsg';
import { createConnection, start, stop,useActiveModuleSync } from "shared-signalr";
import { useCustomStore } from "shared-store";
import { useEffect } from 'react';

export default function App() {
  const user = sessionStorage.getItem('user');
  const userdata = JSON.parse(user);
  useEffect(() => {
    if(!user) {
      stop();
      return;
    }
    // createConnection(userdata);
    // start();
  }, [user]);
  useActiveModuleSync();

  const Auth = React.lazy(() =>
    import('auth/App').then((module) => ({
      default: (props) => <module.default {...props} />,
    }))
  )

  const [isAuthenticated, setIsAuthenticated] = useState(
    !!localStorage.getItem('token')
  )

  return (
    <>
      <ToastContainer
        position="top-center"
        toastOptions={{
          style: {
            fontSize: '16px',
            padding: '16px',
            width: '400px',
            borderRadius: '8px',
          },
        }} />
      <GlobalError />
      <GlobalLoader />
      <GlobalSuccess />
      <CentralRouter />
    </>
  )
}
