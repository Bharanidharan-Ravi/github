import { jwtDecode } from "jwt-decode";
import { decryptUserInfo } from "../Decryption/Decryption";

// #region Decrypt a user details 
export const getDecryptedUser = () => {
    const user = sessionStorage.getItem("user");
    if (!user) return null;
  
    try {
      const encrypted = JSON.parse(user);
      return decryptUserInfo(encrypted)[0]; // <- return it
    } catch (err) {
      console.error("User decryption failed", err);
      return null;
    }
  };
  // #endregion 

  export const isTokenValid = (token) => {
    try {
      const decoded = jwtDecode(token);
      return decoded.exp * 1000 > Date.now();
    } catch {
      return false;
    }
  };