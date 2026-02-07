import api from "../ApiService/Api";
import { decryptUserInfo } from "../Helper/decrypt";
import { useCustomStore } from "../store"

export const loginThunk = async (credentials) => {
    const { login } = useCustomStore.getState();    
    try {
        const response = await api.post(`login`,credentials);
        console.log("response :", response);
        const decrypt = decryptUserInfo(response)[0];
        if (!decrypt) throw new Error("Decryption failed");
        login(decrypt, decrypt?.JwtToken);
        sessionStorage.setItem("user", JSON.stringify(response));
        sessionStorage.setItem('isAuthenticated', true);
        sessionStorage.setItem("role", decrypt?.Role);
        return decrypt;
    }catch (err) {
        // setAuthError(err);
    }
}