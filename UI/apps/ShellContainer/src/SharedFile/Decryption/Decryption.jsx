import CryptoJS from 'crypto-js';

const encryptionKey = import.meta.env.VITE_APP_ENCRYPTION_KEY; // Load the encryption key from environment variables

const decryptUserInfo = (encryptedUserInfo) => {        
    if (!encryptedUserInfo || typeof encryptedUserInfo !== 'string') {
        console.warn("No encrypted user info provided");
        return null;
    }
    const keyBytes = CryptoJS.enc.Utf8.parse(encryptionKey);
    const encrypt = encryptedUserInfo ? encryptedUserInfo : ""
    const encryptedData = CryptoJS.enc.Base64.parse(encrypt);

    // Extract the IV from the encrypted message
    const iv = CryptoJS.lib.WordArray.create(encryptedData.words.slice(0, 4)); // IV is the first 16 bytes (128 bits)
    const encryptedMessage = CryptoJS.lib.WordArray.create(encryptedData.words.slice(4)); // The rest is the encrypted message

    // Decrypt
    const decrypted = CryptoJS.AES.decrypt({ ciphertext: encryptedMessage }, keyBytes, {
        iv: iv,
        mode: CryptoJS.mode.CBC,
        padding: CryptoJS.pad.Pkcs7
    });

    // Parse decrypted data
    const jsonString = decrypted.toString(CryptoJS.enc.Utf8);
    return JSON.parse(jsonString);
};

export { decryptUserInfo };

// const encryptUserInfo = (data) => {
//     if (!data) {
//         console.warn("No user data provided for encryption");
//         return null;
//     }

//     const keyBytes = CryptoJS.enc.Utf8.parse(encryptionKey);
//     const iv = CryptoJS.lib.WordArray.random(16); // 128-bit IV

//     const plaintext = JSON.stringify(data);

//     const encrypted = CryptoJS.AES.encrypt(plaintext, keyBytes, {
//         iv: iv,
//         mode: CryptoJS.mode.CBC,
//         padding: CryptoJS.pad.Pkcs7
//     });

//     // Combine IV and ciphertext: [IV][Ciphertext]
//     const ivAndCiphertext = iv.concat(encrypted.ciphertext);

//     // Base64 encode the result
//     return CryptoJS.enc.Base64.stringify(ivAndCiphertext);
// };

// export { encryptUserInfo };