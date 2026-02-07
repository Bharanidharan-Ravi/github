// import { TextField } from "@mui/material";
import Bowser from "bowser";
import { useEffect, useRef, useState } from "react";
import "./loginPage.css";
import {
    Avatar,
    Button,
    TextField,
    FormControlLabel,
    Checkbox,
    Grid,
    Box,
    Typography,
    Container,
    Paper,
} from '@mui/material';
import LockOutlinedIcon from '@mui/icons-material/LockOutlined';
import { styled } from '@mui/material/styles';
import { loginThunk } from "shared-store";

const YellowButton = styled(Button)(({ theme }) => ({
    backgroundColor: '#f1c40f',
    color: '#000',
    fontWeight: 'bold',
    textTransform: 'none',
    '&:hover': {
        backgroundColor: '#d4ac0d',
    },
}));

const LoginPage = ({ onLogin }) => {
    const [formData, setFormData] = useState({
        username: '',
        password: '',
        remember: false,
    });
    const [usernameError, setUsernameError] = useState("");
    const [passwordError, setPasswordError] = useState("");
    const [shakeField, setShakeField] = useState({ username: false, password: false });
    const userAgent = window.navigator.userAgent;

    const handleChange = (e, setValue, setError, fieldName, characterLimit) => {
        const { name, value, type, checked } = e.target;

        // Check length limit
        if (value.length <= characterLimit) {
            setValue(value);
            setFormData({
                ...formData,
                [name]: type === "checkbox" ? checked : value,
            });
            setError("");
            setShakeField({ ...shakeField, [name]: false }); // stop shaking if valid
        } else {
            setError(`Maximum ${characterLimit} characters allowed for ${fieldName}.`);
            setShakeField({ ...shakeField, [name]: true }); // trigger shake
            setTimeout(() => setShakeField({ ...shakeField, [name]: false }), 300);
        }
    };

    const handleSubmit = async (e) => {
        e.preventDefault();
        let hasError = false;

        if (!formData.username.trim()) {
            setUsernameError("Username is required.");
            hasError = true;
        } else {
            setUsernameError("");
        }

        if (!formData.password.trim()) {
            setPasswordError("Password is required.");
            hasError = true;
        } else {
            setPasswordError("");
        }

        if (hasError) return;

        const browser = Bowser.getParser(userAgent);
        const body = {
            username: formData.username,
            password: formData.password,
            DeviceInfo: JSON.stringify(browser.parsedResult)
        }
        const result = await loginThunk(body);
        if (result) {            
            onLogin(result);
        }
    };

    return (
        <Box
            className="login-container"
        // sx={{
        //   background: 'linear-gradient(135deg, #000000, #1c1c1c)',
        //   height: '100vh',
        //   display: 'flex',
        //   alignItems: 'center',
        //   justifyContent: 'center',
        // }}
        >
            <Container component="main" maxWidth="xs">
                <Paper
                    elevation={10}
                    className="login-paper"
                // sx={{
                //   p: 4,
                //   borderRadius: 4,
                //   backgroundColor: '#121212',
                //   color: '#fff',
                // }}
                >
                    <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center' }}>
                        <Avatar sx={{ m: 1, bgcolor: '#f1c40f' }}>
                            <LockOutlinedIcon sx={{ color: '#000' }} />
                        </Avatar>
                        <Typography component="h1" variant="h5" sx={{ fontWeight: 'bold', mb: 2 }}>
                            Sign In
                        </Typography>

                        <Box component="form" onSubmit={handleSubmit} sx={{ mt: 1 }}>
                            <TextField
                                margin="normal"
                                required
                                fullWidth
                                id="username"
                                label="Username"
                                name="username"
                                autoComplete="username"
                                autoFocus
                                className={shakeField.username ? "shake" : ""}
                                value={formData.username}
                                onChange={(e) =>
                                    handleChange(e, (v) => setFormData({ ...formData, username: v }), setUsernameError, "Username", 20)
                                }
                                error={!!usernameError}
                                helperText={usernameError}
                                InputLabelProps={{ style: { color: "#000" } }}
                                InputProps={{
                                    style: { color: "#000", borderColor: "#f1c40f" },
                                }}
                            />

                            <TextField
                                margin="normal"
                                required
                                fullWidth
                                name="password"
                                label="Password"
                                type="password"
                                id="password"
                                className={shakeField.password ? "shake" : ""}
                                autoComplete="current-password"
                                value={formData.password}
                                onChange={(e) =>
                                    handleChange(e, (v) => setFormData({ ...formData, password: v }), setPasswordError, "Password", 30)
                                }
                                error={!!passwordError}
                                helperText={passwordError}
                                InputLabelProps={{ style: { color: "#000" } }}
                                InputProps={{
                                    style: { color: "#000", borderColor: "#f1c40f" },
                                }}
                            />

                            {/* <FormControlLabel
                  control={
                    <Checkbox
                      name="remember"
                      color="default"
                      checked={formData.remember}
                      onChange={handleChange}
                      sx={{
                        color: '#f1c40f',
                        '&.Mui-checked': { color: '#f1c40f' },
                      }}
                    />
                  }
                  label="Remember me"
                /> */}
                            <YellowButton
                                type="submit"
                                fullWidth
                                variant="contained"
                                sx={{ mt: 3, mb: 2, py: 1.2, borderRadius: 2 }}
                            >
                                Sign In
                            </YellowButton>

                            <Grid container
                                className="forgotpassword">
                                <Grid item xs>
                                    <Typography
                                        variant="body2"
                                        sx={{ cursor: 'pointer' }}
                                    >
                                        Forgot password?
                                    </Typography>
                                </Grid>
                                {/* <Grid item>
                    <Typography
                      variant="body2"
                      sx={{ color: '#f1c40f', cursor: 'pointer' }}
                    >
                      {"Don't have an account? Sign Up"}
                    </Typography>
                  </Grid> */}
                            </Grid>
                        </Box>
                    </Box>
                </Paper>
            </Container>
        </Box >
    );
};
// function LoginPage() {
//     // State to handle form input values and errors
//     const [username, setUsername] = useState("");
//     const [password, setPassword] = useState("");
//     const [usernameError, setUsernameError] = useState("");
//     const [passwordError, setPasswordError] = useState("");
//     const [sessionExpired, setSessionExpired] = useState(false);
//     const userAgent = window.navigator.userAgent;
//     const dispatch = useDispatch();
//     const navigate = useNavigate();
//     // const { isAuthenticated, role, error } = useSelector((state) => state.login);
//     const { isAuthenticated, role, error } = {};
//     const hasNavigated = useRef(false);

//     // Effect for session expiration check
//     useEffect(() => {
//         const expired = localStorage.getItem("sessionExpired") === "true";
//         setSessionExpired(expired);
//         if (expired) {
//             localStorage.removeItem("sessionExpired");
//         }
//     }, []);

//     // Handle tab visibility change
//     useEffect(() => {
//         const handleVisibilityChange = () => {
//             if (document.visibilityState === "visible") {
//                 if (sessionExpired && error) {
//                     Swal.fire({
//                         position: "center",
//                         icon: "error",
//                         title: "Your session has expired. Please log in again.",
//                         showConfirmButton: false,
//                         timer: 5000,
//                     }).then(() => {
//                         localStorage.removeItem("sessionExpired");
//                     });
//                     dispatch(resetAlert());
//                 }
//             } else {
//             }
//         };

//         document.addEventListener("visibilitychange", handleVisibilityChange);

//         return () => {
//             document.removeEventListener("visibilitychange", handleVisibilityChange);
//         };
//     }, [error, dispatch, sessionExpired]);

//     // Handle input changes and validate for character limit
//     const handleInputChange = (e, setValue, setError, characterLimit) => {
//         const inputValue = e.target.value;
//         if (inputValue.length <= characterLimit) {
//             setValue(inputValue);
//             setError(""); 
//         } else {
//             setError(`Character limit exceeded. Maximum allowed: ${characterLimit}`);
//         }
//     };

//     // Handle form submission and dispatch the login action
//     const handleSubmit = (e) => {
//         e.preventDefault();
//         let hasError = false;

//         if (!username.trim()) {
//             setUsernameError("Username is required.");
//             hasError = true;
//         } else {
//             setUsernameError("");
//         }

//         if (!password.trim()) {
//             setPasswordError("Password is required.");
//             hasError = true;
//         } else {
//             setPasswordError("");
//         }

//         if (hasError) return;

//         const browser = Bowser.getParser(userAgent);
//         const body = {
//             username: username,
//             password: password,
//             DeviceInfo: JSON.stringify(browser.parsedResult)
//         }
//         dispatch(loginUser(body));
//     };

//     // Effect to navigate when the user is authenticated
//     useEffect(() => {
//         if (isAuthenticated && !hasNavigated.current) {
//             hasNavigated.current = true;
//             if (role === USER_ROLES.Admin) {
//                 navigate("/Admin/AdminDashboard");
//             } else if (role === USER_ROLES.Doctor) {

//                 navigate("/Doctor/DoctorDashboard");
//             }
//             // navigate("/DashBoard");
//         }
//     }, [isAuthenticated, navigate, role]);


//     return (
//         <>
//         <div className="login-page container-fluid d-flex align-items-center justify-content-center min-vh-100">
//         <div className="w-100 px-3" style={{ position: 'absolute', top: '20px', left: '0' }}>
//         {/* <button
//           className="back-button-login"
//           onClick={() => navigate("/")}
//         >
//           <IoArrowBackCircle size={24} style={{ color: '#4682B4' }}/> Back
//         </button> */}
//       </div>
//           <div className="card shadow p-4" style={{ maxWidth: '400px', width: '100%' }}>
//           <div className="d-flex align-items-center justify-content-center mb-4">
//         <img
//           src="/Tootist Logo.png"
//           alt="WG Logo"
//           style={{ height: '50px', marginRight: '10px' }}
//         />
//         {/* <h2 className="fw-bold m-0">Tootist</h2> */}
//       </div>
//             <form onSubmit={handleSubmit}>
//              <div className="form-floating mb-3">
//                 <TextField
//                     type="text"
//                     variant="standard"
//                     className="form-control"
//                     id="floatingUsername"
//                     placeholder="Username"
//                     autoComplete="off"
//                     value={username}
//                     onChange={(e) => handleInputChange(e, setUsername, setUsernameError, 20)}
//                     required
//                     style={{ outline: "none", boxShadow: "none" }}
//                 />
//                     {/* <label htmlFor="floatingUsername">Username</label> */}
//                     {usernameError && <div className="invalid-feedback">{usernameError}</div>}
//                     </div>

//                         {/* Password input with validation */}
//                         <div className="form-floating mb-3">
//                             <input
//                                 type="password"
//                                 className={`form-control ${passwordError ? "is-invalid shake" : ""}`}
//                                 id="floatingPassword"
//                                 placeholder="Password"
//                                 value={password}
//                                 onChange={(e) => handleInputChange(e, setPassword, setPasswordError, 30)}
//                                 required
//                                 style={{ outline: "none", boxShadow: "none" }}
//                             />
//                             <label htmlFor="floatingPassword">Password</label>
//                             {passwordError && <div className="invalid-feedback">{passwordError}</div>}
//                         </div>

//                         {/* Submit button */}
//                         <button className="btn btn-primary w-100" type="submit">
//                             Login
//                         </button>
//                     </form>
//                 </div>
//             </div>
//         </>
//     );
// }

export default LoginPage;