import React, { createContext, useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
// import { useNavigate } from 'react-router-dom'

export const AuthContext = createContext()

export const AuthProvider = ({ children }) => {

  const [isAuthenticated, setIsAuthenticated] = useState(false)
  const [loading, setLoading] = useState(true)
//   const navigate = useNavigate()

  // Simulate checking auth from localStorage / token
  useEffect(() => {
    const token = localStorage.getItem('token')
    if (token) setIsAuthenticated(true)
    setLoading(false)
  }, [])

  return (
    <AuthContext.Provider value={{ isAuthenticated, loading}}>
      {children}
    </AuthContext.Provider>
  )
}
