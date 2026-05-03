const crypto = require('crypto');

const ALGORITHM = 'aes-256-cbc';
const KEY_LENGTH = 32;
const IV_LENGTH = 16;
const SALT_LENGTH = 16;
const PBKDF2_ITERATIONS = 100000;

function deriveKey(password, salt) {
    return crypto.pbkdf2Sync(password, salt, PBKDF2_ITERATIONS, KEY_LENGTH, 'sha256');
}

function encrypt(data, password) {
    const salt = crypto.randomBytes(SALT_LENGTH);
    const key = deriveKey(password, salt);
    const iv = crypto.randomBytes(IV_LENGTH);

    const cipher = crypto.createCipheriv(ALGORITHM, key, iv);
    const encrypted = Buffer.concat([cipher.update(data, 'utf8'), cipher.final()]);

    const result = Buffer.alloc(salt.length + iv.length + encrypted.length);
    let offset = 0;
    offset += salt.copy(result, offset);
    offset += iv.copy(result, offset);
    encrypted.copy(result, offset);

    return result.toString('base64');
}

function decrypt(encryptedData, password) {
    const buffer = Buffer.from(encryptedData, 'base64');

    const salt = buffer.subarray(0, SALT_LENGTH);
    const iv = buffer.subarray(SALT_LENGTH, SALT_LENGTH + IV_LENGTH);
    const encrypted = buffer.subarray(SALT_LENGTH + IV_LENGTH);
    const key = deriveKey(password, salt);

    const decipher = crypto.createDecipheriv(ALGORITHM, key, iv);

    return Buffer.concat([decipher.update(encrypted), decipher.final()]).toString('utf8');
}

function encryptFile(fileBuffer, password) {
    const salt = crypto.randomBytes(SALT_LENGTH);
    const key = deriveKey(password, salt);
    const iv = crypto.randomBytes(IV_LENGTH);

    const cipher = crypto.createCipheriv(ALGORITHM, key, iv);
    const encrypted = Buffer.concat([cipher.update(fileBuffer), cipher.final()]);

    const result = Buffer.alloc(salt.length + iv.length + encrypted.length);
    let offset = 0;
    offset += salt.copy(result, offset);
    offset += iv.copy(result, offset);
    encrypted.copy(result, offset);

    return result;
}

function decryptFile(encryptedBuffer, password) {
    const salt = encryptedBuffer.subarray(0, SALT_LENGTH);
    const iv = encryptedBuffer.subarray(SALT_LENGTH, SALT_LENGTH + IV_LENGTH);
    const encrypted = encryptedBuffer.subarray(SALT_LENGTH + IV_LENGTH);

    const key = deriveKey(password, salt);

    const decipher = crypto.createDecipheriv(ALGORITHM, key, iv);

    return Buffer.concat([decipher.update(encrypted), decipher.final()]);
}

module.exports = {
    encrypt,
    decrypt,
    encryptFile,
    decryptFile,
    ALGORITHM,
    KEY_LENGTH,
    IV_LENGTH
};
