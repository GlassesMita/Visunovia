const crypto = require('crypto');

const SALT = 'Visunovia_LoR_2024_Simple';
const ITERATIONS = 100000;
const KEY_LENGTH = 32;
const IV_LENGTH = 16;
const XOR_KEY = 0x4E;

const OBFUSCATED_KEY_STRING = 'PLACEHOLDER';

function restoreRawKeyString() {
    const obfuscatedBytes = Buffer.from(OBFUSCATED_KEY_STRING, 'base64');
    const base64Bytes = Buffer.alloc(obfuscatedBytes.length);
    for (let i = 0; i < obfuscatedBytes.length; i++) {
        base64Bytes[i] = obfuscatedBytes[i] ^ XOR_KEY;
    }
    const base64String = base64Bytes.toString('utf8');
    return Buffer.from(base64String, 'base64').toString('utf8');
}

function deriveAesKey(rawKeyString) {
    const keyBytes = Buffer.from(rawKeyString, 'utf8');
    const saltBytes = Buffer.from(SALT, 'utf8');
    return crypto.pbkdf2Sync(keyBytes, saltBytes, ITERATIONS, KEY_LENGTH, 'sha256');
}

function decryptPackage(lorpkgPath) {
    const rawKeyString = restoreRawKeyString();
    const key = deriveAesKey(rawKeyString);

    const input = require('fs').readFileSync(lorpkgPath);
    const iv = input.slice(0, IV_LENGTH);
    const encrypted = input.slice(IV_LENGTH);

    const decipher = crypto.createDecipheriv('aes-256-cbc', key, iv);
    return Buffer.concat([decipher.update(encrypted), decipher.final()]);
}

function decryptBytes(encryptedData) {
    const rawKeyString = restoreRawKeyString();
    const key = deriveAesKey(rawKeyString);

    const iv = encryptedData.slice(0, IV_LENGTH);
    const encrypted = encryptedData.slice(IV_LENGTH);

    const decipher = crypto.createDecipheriv('aes-256-cbc', key, iv);
    return Buffer.concat([decipher.update(encrypted), decipher.final()]);
}

module.exports = {
    decryptPackage,
    decryptBytes,
    restoreRawKeyString,
    deriveAesKey,
    OBFUSCATED_KEY_STRING
};
