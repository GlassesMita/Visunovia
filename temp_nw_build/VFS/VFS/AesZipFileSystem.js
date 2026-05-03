const fs = require('fs');
const path = require('path');
const AdmZip = require('adm-zip');
const AesUtil = require('./AesUtil');

class AesZipFileSystem {
    constructor() {
        this.zipPath = null;
        this.password = null;
        this.zipHandle = null;
        this.fileEntries = new Map();
        this.manifest = null;
        this.isMemoryBased = false;
        this.memoryBuffer = null;
    }

    mount(zipPath, password) {
        if (!fs.existsSync(zipPath)) {
            throw new Error(`ZIP 文件不存在: ${zipPath}`);
        }

        this.zipPath = zipPath;
        this.password = password;

        try {
            const zipBuffer = fs.readFileSync(zipPath);
            const decryptedBuffer = AesUtil.decryptFile(zipBuffer, password);
            this.memoryBuffer = decryptedBuffer;
            this.isMemoryBased = true;
            this.zipHandle = new AdmZip(decryptedBuffer);

            this._loadManifest();
            return true;
        } catch (error) {
            if (error.message.includes('Unsupported state') || error.message.includes('auth')) {
                throw new Error('密码错误或文件已损坏');
            }
            throw error;
        }
    }

    mountFromBuffer(buffer, password) {
        if (!buffer || buffer.length === 0) {
            throw new Error('缓冲区为空');
        }

        this.isMemoryBased = true;
        this.memoryBuffer = buffer;

        try {
            const decryptedBuffer = AesUtil.decryptFile(buffer, password);
            this.memoryBuffer = decryptedBuffer;
            this.zipHandle = new AdmZip(decryptedBuffer);

            this._loadManifest();
            return true;
        } catch (error) {
            if (error.message.includes('Unsupported state') || error.message.includes('auth')) {
                throw new Error('密码错误或文件已损坏');
            }
            throw error;
        }
    }

    mountFromZipBuffer(buffer) {
        if (!buffer || buffer.length === 0) {
            throw new Error('缓冲区为空');
        }

        this.isMemoryBased = true;
        this.memoryBuffer = buffer;

        try {
            this.zipHandle = new AdmZip(buffer);
            this._loadManifest();
            return true;
        } catch (error) {
            throw error;
        }
    }

    _loadManifest() {
        try {
            const manifestEntry = this.zipHandle.getEntry('project.manifest');
            if (manifestEntry) {
                const content = manifestEntry.getData().toString('utf8');
                this.manifest = JSON.parse(content);
            }
        } catch (error) {
            console.warn('无法加载 manifest:', error.message);
        }
    }

    readFile(virtualPath) {
        if (!this.zipHandle) {
            throw new Error('VFS 未挂载');
        }

        const normalizedPath = this._normalizePath(virtualPath);
        const entry = this.zipHandle.getEntry(normalizedPath);

        if (!entry) {
            return null;
        }

        if (entry.isDirectory) {
            return null;
        }

        const data = entry.getData();
        const ext = path.extname(normalizedPath).toLowerCase();

        if (['.json', '.xml', '.yaml', '.yml', '.lor', '.tlor', '.txt', '.html', '.css', '.js', '.md'].includes(ext)) {
            return data.toString('utf8');
        }

        return data;
    }

    readFileBuffer(virtualPath) {
        if (!this.zipHandle) {
            throw new Error('VFS 未挂载');
        }

        const normalizedPath = this._normalizePath(virtualPath);
        const entry = this.zipHandle.getEntry(normalizedPath);

        if (!entry || entry.isDirectory) {
            return null;
        }

        return entry.getData();
    }

    fileExists(virtualPath) {
        if (!this.zipHandle) {
            return false;
        }

        const normalizedPath = this._normalizePath(virtualPath);
        const entry = this.zipHandle.getEntry(normalizedPath);
        return entry !== null && !entry.isDirectory;
    }

    directoryExists(virtualPath) {
        if (!this.zipHandle) {
            return false;
        }

        const normalizedPath = this._normalizePath(virtualPath);
        const entries = this.zipHandle.getEntries();

        for (const entry of entries) {
            if (entry.entryName.startsWith(normalizedPath)) {
                return true;
            }
        }
        return false;
    }

    listFiles(virtualDir) {
        if (!this.zipHandle) {
            return [];
        }

        const normalizedDir = this._normalizePath(virtualDir);
        const results = [];
        const entries = this.zipHandle.getEntries();

        for (const entry of entries) {
            if (entry.isDirectory) continue;

            const entryDir = path.dirname(entry.entryName);
            if (entryDir === normalizedDir || entryDir.startsWith(normalizedDir + '/')) {
                const relativePath = path.relative(normalizedDir, entry.entryName);
                if (!relativePath.includes('/')) {
                    results.push({
                        name: path.basename(entry.entryName),
                        path: entry.entryName,
                        size: entry.header.size
                    });
                }
            }
        }

        return results;
    }

    listDirectories(virtualDir) {
        if (!this.zipHandle) {
            return [];
        }

        const normalizedDir = this._normalizePath(virtualDir);
        const directories = new Set();
        const entries = this.zipHandle.getEntries();

        for (const entry of entries) {
            if (entry.isDirectory) continue;

            const entryDir = path.dirname(entry.entryName);
            if (entryDir.startsWith(normalizedDir + '/')) {
                const relativeDir = entryDir.substring(normalizedDir.length + 1);
                const firstDir = relativeDir.split('/')[0];
                if (firstDir) {
                    directories.add(firstDir);
                }
            }
        }

        return Array.from(directories).map(name => ({
            name: name,
            path: path.join(normalizedDir, name)
        }));
    }

    getProjectData() {
        if (!this.zipHandle) {
            throw new Error('VFS 未挂载');
        }

        const projectFile = this.readFile('Project.tlor') || this.readFile('project.json');
        if (!projectFile) {
            return null;
        }

        try {
            return JSON.parse(projectFile);
        } catch {
            return null;
        }
    }

    getProjectPath() {
        return this.zipPath;
    }

    unmount() {
        if (this.zipHandle) {
            this.zipHandle = null;
        }
        this.fileEntries.clear();
        this.manifest = null;
        this.zipPath = null;
        this.password = null;
        this.isMemoryBased = false;
        this.memoryBuffer = null;
    }

    _normalizePath(virtualPath) {
        let normalized = virtualPath.replace(/\\/g, '/');
        normalized = normalized.replace(/\/+/g, '/');
        if (normalized.startsWith('/')) {
            normalized = normalized.substring(1);
        }
        if (normalized.endsWith('/')) {
            normalized = normalized.slice(0, -1);
        }
        return normalized;
    }
}

module.exports = AesZipFileSystem;
