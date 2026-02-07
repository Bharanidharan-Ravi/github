import React from 'react';
import { Routes, Route, BrowserRouter } from 'react-router-dom'; // Wrap Routes with Router
import LoginPage from './components/LoginPage/loginPage';

function App({ onLogin }) {
  const isStandalone = !window.__MICRO_FRONTEND__;
  return (
    <div> {/* Make sure the app is wrapped in Router */}
      {isStandalone ? (
        <BrowserRouter> {/* Only wrap in BrowserRouter if running standalone */}
          <Routes>
            <Route path="/" element={<LoginPage onLogin={onLogin} />} />
          </Routes>
        </BrowserRouter>
      ) : (
        <Routes>
          <Route path="/" element={<LoginPage onLogin={onLogin} />} />
        </Routes>
      )}
    </div>
  );
}

export default App;
