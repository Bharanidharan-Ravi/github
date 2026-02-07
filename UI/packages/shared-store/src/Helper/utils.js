import { decryptUserInfo } from "./decrypt";

// #region Decrypt a user details 
export const getDecryptedUser = () => {
    try {
        const user = sessionStorage.getItem("user");        
        if (!user) return null;

        try {
            const encrypted = JSON.parse(user);
           if(typeof encrypted === "string") return decryptUserInfo(encrypted)[0]; // <- return it
           return null;
        } catch (err) {
            console.error("User decryption failed", err);
            return null;
        }
    } catch (error) {
        console.error("User decryption failed", err);
        return null;
    }
};
// #endregion 