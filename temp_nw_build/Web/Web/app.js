const fs = require('fs');
const path = require('path');

const { xorDecrypt } = require('../Security/simple-crypto.js');

let renderer = null;
let currentPassword = null;

function showScreen(screenId) {
    const screens = ['loading', 'password-dialog', 'main-container', 'error-screen', 'select-screen'];
    screens.forEach(id => {
        const el = document.getElementById(id);
        if (el) {
            el.classList.toggle('hidden', id !== screenId);
        }
    });
}

function showError(message) {
    const errorEl = document.getElementById('error-message');
    if (errorEl) {
        errorEl.textContent = message;
    }
    showScreen('error-screen');
}

function updateLoadingText(text) {
    const el = document.querySelector('.loading-text');
    if (el) el.textContent = text;
}

async function loadProject(filePath, password) {
    try {
        showScreen('loading');
        updateLoadingText('正在解密资源包...');

        const encryptedData = fs.readFileSync(filePath);
        const decryptedData = xorDecrypt(encryptedData, password);

        updateLoadingText('正在解析项目数据...');

        const zipData = await parseZip(decryptedData);

        const projectJsonEntry = zipData.find(e => e.filename === 'project.json');
        if (!projectJsonEntry) {
            throw new Error('无法找到 project.json，项目格式不正确');
        }

        const projectData = JSON.parse(new TextDecoder('utf8').decode(projectJsonEntry.data));

        const assets = {};
        for (const entry of zipData) {
            if (entry.filename.startsWith('assets/')) {
                assets[entry.filename] = entry.data;
            }
        }

        currentPassword = password;

        if (!renderer) {
            renderer = new window.Renderer();
        }

        renderer.loadProject(projectData, {}, assets);
        showScreen('main-container');

    } catch (error) {
        console.error('Failed to load project:', error);
        if (error.message.includes('Invalid') || error.message.includes('corrupted')) {
            showError('项目文件损坏，无法解析');
        } else {
            showError('密码错误或文件损坏');
        }
    }
}

async function parseZip(buffer) {
    const entries = [];
    const view = new DataView(buffer.buffer, buffer.byteOffset, buffer.byteLength);

    if (buffer.length < 4 || view.getUint32(0, true) !== 0x04034b50) {
        throw new Error('Invalid ZIP file');
    }

    let offset = 0;
    while (offset < buffer.length) {
        const sig = view.getUint32(offset, true);
        if (sig === 0x02014b50 || sig === 0x06054b50) break;
        if (sig !== 0x04034b50) {
            offset++;
            continue;
        }

        const compression = view.getUint16(offset + 8, true);
        const compSize = view.getUint32(offset + 18, true);
        const uncompSize = view.getUint32(offset + 22, true);
        const nameLen = view.getUint16(offset + 26, true);
        const extraLen = view.getUint16(offset + 28, true);

        const nameBytes = new Uint8Array(buffer.buffer, buffer.byteOffset + offset + 30, nameLen);
        const filename = new TextDecoder('utf8').decode(nameBytes);

        const dataStart = offset + 30 + nameLen + extraLen;
        const compressedData = new Uint8Array(buffer.buffer, buffer.byteOffset + dataStart, compSize);

        let content;
        if (compression === 0) {
            content = compressedData;
        } else if (compression === 8) {
            content = await inflateBuffer(compressedData);
        } else {
            offset = dataStart + compSize;
            continue;
        }

        entries.push({ filename, data: content });
        offset = dataStart + compSize;
    }

    return entries;
}

function inflateBuffer(data) {
    return new Promise((resolve, reject) => {
        try {
            const pako = require('pako');
            const result = pako.inflate(data);
            resolve(new Uint8Array(result));
        } catch (e) {
            reject(e);
        }
    });
}

function showPasswordDialog(filePath) {
    showScreen('password-dialog');

    const input = document.getElementById('password-input');
    const confirmBtn = document.getElementById('password-confirm-btn');

    if (input) input.value = '';

    const handleConfirm = () => {
        const password = input ? input.value : '';
        if (!password) {
            alert('请输入密码');
            return;
        }
        confirmBtn.removeEventListener('click', handleConfirm);
        loadProject(filePath, password);
    };

    confirmBtn.addEventListener('click', handleConfirm);

    if (input) {
        input.addEventListener('keypress', (e) => {
            if (e.key === 'Enter') {
                handleConfirm();
            }
        });
        input.focus();
    }
}

document.addEventListener('DOMContentLoaded', () => {
    renderer = new window.Renderer();

    const lorePath = path.join(__dirname, 'Game.lore');
    if (fs.existsSync(lorePath)) {
        const password = require('../Security/simple-crypto.js').restoreRawKeyString();
        loadProject(lorePath, password);
    } else {
        const openBtn = document.getElementById('open-project-btn');
        if (openBtn) {
            openBtn.addEventListener('click', async () => {
                const { dialog } = require('electron').remote;
                const result = await dialog.showOpenDialog({
                    filters: [{ name: 'Game Files', extensions: ['lore'] }],
                    properties: ['openFile']
                });
                if (!result.canceled && result.filePaths.length > 0) {
                    const password = require('../Security/simple-crypto.js').restoreRawKeyString();
                    loadProject(result.filePaths[0], password);
                }
            });
        }
        showScreen('select-screen');
    }

    const dialogueBox = document.getElementById('dialogue-box');
    if (dialogueBox) {
        dialogueBox.addEventListener('click', () => {
            if (renderer) {
                renderer.advance();
            }
        });
    }

    const closeBtn = document.getElementById('close-btn');
    if (closeBtn) {
        closeBtn.addEventListener('click', () => {
            window.close();
        });
    }

    const replayBtn = document.getElementById('replay-btn');
    if (replayBtn) {
        replayBtn.addEventListener('click', () => {
            if (renderer) {
                renderer.reset();
            }
        });
    }

    const retryBtn = document.getElementById('retry-btn');
    if (retryBtn) {
        retryBtn.addEventListener('click', () => {
            showScreen('select-screen');
        });
    }
});
