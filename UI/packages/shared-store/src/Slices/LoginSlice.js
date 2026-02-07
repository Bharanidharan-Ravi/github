import { getDecryptedUser } from "../Helper/utils";

export const createAuthSlice = (set, get) => ({
    user: null,
    token: null,
    role: null,
    isAuthenticated: false,

    hydrateUserdata: () => {
        const u = getDecryptedUser();        
        if (u) {
            set({
                user: u,
                token: u.JwtToken ?? null,
                role: u.Role ?? null,
                isAuthenticated: Boolean(u.JwtToken),
            });
        } else {
            set({user: null, token: null, role: null, isAuthenticated: false});
        }
    },

    login: (decryptedUser, token) => {
        set({
            user: decryptedUser,
            token: token ?? decryptedUser?.JwtToken ?? null,
            role: decryptedUser?.Role ?? null,
            isAuthenticated: true,
        });
    },

    logout: () => {
        sessionStorage.removeItem("user");
        set({user: null, token: null, role: null, isAuthenticated: false});
    },

    isTokenExpired: () => {
        const token = get().token;
        if (!token) return true;
         try {
            const payload = JSON.parse(atob(token.split(".")[1]));
            return Date.now() > payload.exp * 1000;
         } catch (error) {
            return true;
            
         }
    }, 
});