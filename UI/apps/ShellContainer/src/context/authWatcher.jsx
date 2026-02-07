// import { useEffect, useContext } from 'react'
// import { useNavigate } from 'react-router-dom'
// import { AuthContext } from './AuthContext'

// export default function AuthWatcher() {
//   const { isAuthenticated, loading } = useContext(AuthContext)
//   const navigate = useNavigate()

//   useEffect(() => {
//     if (!isAuthenticated) {
//       navigate('/auth')
//     }
//   }, [isAuthenticated, loading, navigate])

//   return null
// }
