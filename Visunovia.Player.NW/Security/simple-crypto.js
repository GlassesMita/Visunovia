const OBFUSCATED_KEY_STRING = 'PLACEHOLDER';

function restoreRawKeyString() {
    return OBFUSCATED_KEY_STRING;
}

function xorEncrypt(data, password) {
    const passwordBytes = Buffer.from(password, 'utf8');
    const result = Buffer.alloc(data.length);
    for (let i = 0; i < data.length; i++) {
        result[i] = data[i] ^ passwordBytes[i % passwordBytes.length];
    }
    return result;
}

function xorDecrypt(encryptedData, password) {
    return xorEncrypt(encryptedData, password);
}

function decryptPackage(lorpkgPath, password) {
    const fs = require('fs');
    const encryptedData = fs.readFileSync(lorpkgPath);
    return xorDecrypt(encryptedData, password);
}

function decryptBytes(encryptedData, password) {
    return xorDecrypt(encryptedData, password);
}

function decryptArrayBuffer(arrayBuffer, password) {
    const encryptedData = Buffer.from(arrayBuffer);
    const decrypted = xorDecrypt(encryptedData, password);
    return decrypted.buffer.slice(decrypted.byteOffset, decrypted.byteOffset + decrypted.byteLength);
}

module.exports = {
    decryptPackage,
    decryptBytes,
    decryptArrayBuffer,
    restoreRawKeyString,
    xorEncrypt,
    xorDecrypt,
    OBFUSCATED_KEY_STRING
};
