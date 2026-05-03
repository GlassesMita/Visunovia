const fs = require('fs');
const path = require('path');
const gui = require('nw.gui');

const AesZipFileSystem = require('./VFS/AesZipFileSystem');
const SimpleCrypto = require('./Security/simple-crypto');

let vfs = null;
let currentProjectPath = null;
let win = null;

function init() {
    win = gui.Window.get();

    win.on('loaded', () => {
        console.log('NW.js 窗口加载完成');
    });

    win.on('closed', () => {
        if (vfs) {
            vfs.unmount();
            vfs = null;
        }
    });

    createMenu();
}

function createMenu() {
    const menu = new gui.Menu();

    const fileMenu = new gui.Menu();
    fileMenu.append(new gui.MenuItem({
        label: '打开项目...',
        click: () => openProjectDialog()
    }));
    fileMenu.append(new gui.MenuItem({ type: 'separator' }));
    fileMenu.append(new gui.MenuItem({
        label: '退出',
        click: () => win.close()
    }));

    const playMenu = new gui.Menu();
    playMenu.append(new gui.MenuItem({
        label: '重新开始',
        click: () => sendToRenderer('player:restart')
    }));
    playMenu.append(new gui.MenuItem({
        label: '暂停/继续',
        click: () => sendToRenderer('player:toggle')
    }));

    const windowMenu = new gui.Menu();
    windowMenu.append(new gui.MenuItem({
        label: '最小化',
        click: () => win.minimize()
    }));
    windowMenu.append(new gui.MenuItem({
        label: '最大化',
        click: () => {
            if (win.isMaximized) {
                win.unmaximize();
            } else {
                win.maximize();
            }
        }
    }));
    windowMenu.append(new gui.MenuItem({
        label: '全屏',
        click: () => {
            win.toggleFullscreen();
        }
    }));

    const helpMenu = new gui.Menu();
    helpMenu.append(new gui.MenuItem({
        label: '关于',
        click: () => showAboutDialog()
    }));
    helpMenu.append(new gui.MenuItem({
        label: '开发者工具',
        click: () => win.showDevTools()
    }));

    menu.append(new gui.MenuItem({ label: '文件', submenu: fileMenu }));
    menu.append(new gui.MenuItem({ label: '播放', submenu: playMenu }));
    menu.append(new gui.MenuItem({ label: '窗口', submenu: windowMenu }));
    menu.append(new gui.MenuItem({ label: '帮助', submenu: helpMenu }));

    gui.Menu.setMenu(menu);
}

function openProjectDialog() {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = '.lorpkg,.zip,.tlor,.lore';

    input.onchange = async (e) => {
        const file = e.target.files[0];
        if (file) {
            mountProject(file.path, null);
        }
    };

    input.click();
}

function mountProject(projectPath, password) {
    try {
        vfs = new AesZipFileSystem();

        const ext = path.extname(projectPath).toLowerCase();
        const isEncryptedPackage = ext === '.lore' || ext === '.lorpkg';

        if (isEncryptedPackage) {
            const decryptedBuffer = SimpleCrypto.decryptBytes(fs.readFileSync(projectPath));
            vfs.mountFromZipBuffer(decryptedBuffer);
        } else if (password) {
            vfs.mount(projectPath, password);
        } else {
            vfs.mountFromZipBuffer(fs.readFileSync(projectPath));
        }

        currentProjectPath = projectPath;

        const projectData = vfs.getProjectData();
        if (!projectData) {
            sendToRenderer('project:error', { message: '无法加载项目数据' });
            return;
        }

        const scripts = loadAllScripts(vfs, projectData);
        const assets = loadAssetManifest(vfs);

        sendToRenderer('project:loaded', {
            projectData: projectData,
            projectPath: projectPath,
            scripts: scripts,
            assets: assets
        });

    } catch (error) {
        console.error('加载项目失败:', error);
        sendToRenderer('project:error', {
            message: error.message || '加载项目失败'
        });
    }
}

function loadAllScripts(vfs, projectData) {
    const scripts = {};

    if (projectData.scenes) {
        for (const scene of projectData.scenes) {
            const sceneId = scene.id || scene;
            const scriptPath = `Scripts/Main/${sceneId}.lor`;

            const scriptContent = vfs.readFile(scriptPath);
            if (scriptContent) {
                scripts[sceneId] = scriptContent;
            }
        }
    }

    return scripts;
}

function loadAssetManifest(vfs) {
    const assets = {
        backgrounds: [],
        characters: [],
        musics: [],
        sounds: []
    };

    try {
        const bgDir = 'Assets/Backgrounds';
        if (vfs.directoryExists(bgDir)) {
            assets.backgrounds = vfs.listFiles(bgDir);
        }

        const charDir = 'Assets/Characters';
        if (vfs.directoryExists(charDir)) {
            assets.characters = vfs.listFiles(charDir);
        }

        const musicDir = 'Assets/Musics';
        if (vfs.directoryExists(musicDir)) {
            assets.musics = vfs.listFiles(musicDir);
        }

        const soundDir = 'Assets/Sounds';
        if (vfs.directoryExists(soundDir)) {
            assets.sounds = vfs.listFiles(soundDir);
        }
    } catch (error) {
        console.warn('加载资源清单失败:', error.message);
    }

    return assets;
}

function sendToRenderer(channel, data) {
    if (win && win.window) {
        win.window.postMessage({ channel: channel, data: data }, '*');
    }
}

function showAboutDialog() {
    const aboutWindow = gui.Window.open('data:text/html,<html><body style="background:#0D0D0D;color:white;font-family:Segoe UI;padding:40px;text-align:center;"><h1>Visunovia Player</h1><p>版本 1.0.0</p><p>基于 NW.js 开发</p></body></html>', {
        title: '关于',
        width: 400,
        height: 200,
        resizable: false
    });
}

const args = process.argv.slice(1);
const projectArg = args.find(arg => arg.startsWith('--project='));
if (projectArg) {
    const projectPath = projectArg.split('=')[1];
    setTimeout(() => {
        if (vfs) {
            const projectData = vfs.getProjectData();
            if (projectData) {
                sendToRenderer('project:auto-loaded', {
                    projectData: projectData,
                    projectPath: projectPath
                });
            }
        }
    }, 1000);
}

init();
