class Renderer {
    constructor() {
        this.project = null;
        this.scripts = {};
        this.assets = null;
        this.currentScene = null;
        this.currentDialogueIndex = -1;
        this.isShowingChoices = false;
        this.isEnded = false;
        this.isTextAnimating = false;
        this.isPaused = false;
        this.typewriterInterval = null;
        this.startIndex = 0;
        this.currentSceneIndex = 0;
        this.localMode = false;
        this.localProjectPath = null;
    }

    async loadProject(projectData, scripts = {}, assets = null) {
        this.project = projectData;
        this.scripts = scripts;
        this.assets = assets;
        this.startIndex = projectData.startIndex || 0;
        this.currentSceneIndex = 0;
        this.localMode = false;

        if (this.project.scenes && this.project.scenes.length > 0) {
            this.goToScene(0);
        } else {
            this.showError('项目中没有场景');
        }
    }

    async loadProjectFromLocal(projectPath) {
        this.localMode = true;
        this.localProjectPath = projectPath;

        try {
            const response = await fetch('/api/project');
            if (!response.ok) {
                throw new Error('无法从服务器获取项目数据');
            }

            const data = await response.json();
            const projectData = JSON.parse(data.projectJson);

            if (!projectData || !projectData.scenes || projectData.scenes.length === 0) {
                throw new Error('无法加载项目数据');
            }

            this.project = projectData;
            this.startIndex = data.startIndex || 0;
            this.currentSceneIndex = 0;
            this.goToScene(0);

        } catch (error) {
            this.showError(error.message || '加载项目失败');
        }
    }

    goToScene(sceneIndex) {
        if (!this.project.scenes || sceneIndex >= this.project.scenes.length) {
            this.showEnd();
            return;
        }

        this.currentSceneIndex = sceneIndex;
        this.currentScene = this.project.scenes[sceneIndex];
        this.currentDialogueIndex = this.startIndex > 0 ? this.startIndex - 1 : -1;
        this.isShowingChoices = false;
        this.isEnded = false;
        this.startIndex = 0;

        if (this.currentScene.dialogues && this.currentScene.dialogues.length === 0) {
            const sceneId = this.currentScene.id;
            const scriptContent = this.scripts[sceneId];
            if (scriptContent) {
                this.currentScene.dialogues = this.parseScript(scriptContent);
            }
        }

        this.loadBackground();
        this.advance();
    }

    parseScript(scriptContent) {
        const dialogues = [];
        const lines = scriptContent.split('\n');

        for (const line of lines) {
            const trimmed = line.trim();
            if (!trimmed || trimmed.startsWith('#')) continue;

            if (trimmed.startsWith('bg:')) {
                this.currentScene.background = trimmed.substring(3).trim();
            } else if (trimmed.startsWith('bgm:')) {
                const bgmPath = trimmed.substring(4).trim();
                const parts = bgmPath.split('|');
                this.currentScene.bgm = { path: parts[0].trim() };
                if (parts.length > 1 && !isNaN(parts[1].trim())) {
                    this.currentScene.bgm.volume = parseInt(parts[1].trim());
                }
            } else if (trimmed.includes(':')) {
                const colonIndex = trimmed.indexOf(':');
                const speaker = trimmed.substring(0, colonIndex).trim();
                const text = trimmed.substring(colonIndex + 1).trim();

                dialogues.push({
                    type: 'dialogue',
                    speaker: speaker,
                    text: text
                });
            }
        }

        return dialogues;
    }

    loadBackground() {
        const bgImage = document.getElementById('background-image');
        const bgPath = this.currentScene?.background;

        if (!bgPath) {
            bgImage.src = '';
            return;
        }

        if (this.localMode) {
            const fullPath = 'file:///' + bgPath.replace(/\\/g, '/');
            bgImage.src = fullPath;
        } else if (window.playerAPI) {
            this.loadAsset(bgPath).then(dataUrl => {
                bgImage.src = dataUrl || '';
            }).catch(() => {
                bgImage.src = '';
            });
        }
    }

    async loadAsset(assetPath) {
        if (!assetPath) return null;

        if (window.playerAPI) {
            const result = await window.playerAPI.readFile(assetPath);
            if (result.success && result.content) {
                const ext = assetPath.toLowerCase().split('.').pop();
                const mimeType = this.getMimeType(ext);
                return `data:${mimeType};base64,${this.arrayBufferToBase64(result.content)}`;
            }
        }
        return null;
    }

    getMimeType(ext) {
        const mimeTypes = {
            'png': 'image/png',
            'jpg': 'image/jpeg',
            'jpeg': 'image/jpeg',
            'gif': 'image/gif',
            'webp': 'image/webp',
            'mp3': 'audio/mpeg',
            'wav': 'audio/wav',
            'ogg': 'audio/ogg'
        };
        return mimeTypes[ext] || 'application/octet-stream';
    }

    arrayBufferToBase64(buffer) {
        if (typeof buffer === 'string') {
            return btoa(unescape(encodeURIComponent(buffer)));
        }
        const bytes = new Uint8Array(buffer);
        let binary = '';
        for (let i = 0; i < bytes.byteLength; i++) {
            binary += String.fromCharCode(bytes[i]);
        }
        return btoa(binary);
    }

    advance() {
        if (!this.currentScene || this.isShowingChoices || this.isPaused) return;

        this.currentDialogueIndex++;

        if (this.currentDialogueIndex >= this.currentScene.dialogues.length) {
            this.showEnd();
            return;
        }

        this.clearTypewriter();
        const dialogue = this.currentScene.dialogues[this.currentDialogueIndex];
        this.displayDialogue(dialogue);
    }

    displayDialogue(dialogue) {
        const speakerEl = document.getElementById('speaker-name');
        const textEl = document.getElementById('dialogue-text');
        const dialogueBox = document.getElementById('dialogue-box');
        const choicePanel = document.getElementById('choice-panel');
        const spriteLayer = document.getElementById('sprite-layer');

        choicePanel.classList.add('hidden');
        dialogueBox.classList.remove('hidden');

        if (dialogue.type === 'branch' || dialogue.choices) {
            this.showChoices(dialogue);
            return;
        }

        speakerEl.textContent = dialogue.speaker || '';
        textEl.textContent = '';

        if (dialogue.sprites && dialogue.sprites.length > 0) {
            this.loadSprites(dialogue.sprites);
        } else {
            spriteLayer.innerHTML = '';
        }

        this.animateText(dialogue.text, textEl, () => {
            this.isTextAnimating = false;
        });
    }

    showChoices(dialogue) {
        const choicePanel = document.getElementById('choice-panel');
        const dialogueBox = document.getElementById('dialogue-box');
        const choiceButtons = document.getElementById('choice-buttons');

        dialogueBox.classList.add('hidden');
        choicePanel.classList.remove('hidden');
        this.isShowingChoices = true;

        choiceButtons.innerHTML = '';

        const choices = dialogue.choices || [];

        if (choices.length > 0) {
            choices.forEach((choice, index) => {
                const btn = document.createElement('button');
                btn.className = 'choice-btn';
                btn.textContent = choice.text || choice.label || `选项 ${index + 1}`;
                btn.onclick = () => this.selectChoice(index, choice);
                choiceButtons.appendChild(btn);
            });
        }
    }

    selectChoice(index, choice) {
        this.isShowingChoices = false;
        const choicePanel = document.getElementById('choice-panel');
        choicePanel.classList.add('hidden');

        if (choice.targetScene) {
            const targetIndex = this.project.scenes.findIndex(s =>
                s.id === choice.targetScene || s.id === choice.target
            );
            if (targetIndex >= 0) {
                this.goToScene(targetIndex);
                return;
            }
        }

        if (choice.targetIndex !== undefined) {
            this.goToScene(choice.targetIndex);
            return;
        }

        this.advance();
    }

    async loadSprites(spritePaths) {
        const spriteLayer = document.getElementById('sprite-layer');
        spriteLayer.innerHTML = '';

        for (const spritePath of spritePaths) {
            const img = document.createElement('img');

            if (this.localMode) {
                img.src = 'file:///' + spritePath.replace(/\\/g, '/');
            } else if (window.playerAPI) {
                const dataUrl = await this.loadAsset(spritePath);
                img.src = dataUrl || '';
            }

            img.onerror = () => {
                img.style.display = 'none';
            };

            spriteLayer.appendChild(img);
        }
    }

    animateText(text, element, onComplete) {
        this.isTextAnimating = true;
        let index = 0;
        element.textContent = '';

        this.typewriterInterval = setInterval(() => {
            if (index < text.length) {
                element.textContent += text[index];
                index++;
            } else {
                this.clearTypewriter();
                if (onComplete) onComplete();
            }
        }, 30);
    }

    clearTypewriter() {
        if (this.typewriterInterval) {
            clearInterval(this.typewriterInterval);
            this.typewriterInterval = null;
        }
        this.isTextAnimating = false;
    }

    skipTextAnimation() {
        if (this.isTextAnimating && this.currentScene) {
            const dialogue = this.currentScene.dialogues[this.currentDialogueIndex];
            const textEl = document.getElementById('dialogue-text');
            this.clearTypewriter();
            textEl.textContent = dialogue.text;
            this.isTextAnimating = false;
        }
    }

    togglePause() {
        this.isPaused = !this.isPaused;
        if (this.isPaused) {
            this.clearTypewriter();
        }
    }

    showEnd() {
        this.isEnded = true;
        const dialogueBox = document.getElementById('dialogue-box');
        const choicePanel = document.getElementById('choice-panel');
        const endScreen = document.getElementById('end-screen');

        dialogueBox.classList.add('hidden');
        choicePanel.classList.add('hidden');
        endScreen.classList.remove('hidden');
    }

    showError(message) {
        const loading = document.getElementById('loading');
        const mainContainer = document.getElementById('main-container');
        const selectScreen = document.getElementById('select-screen');
        const errorScreen = document.getElementById('error-screen');
        const errorMessage = document.getElementById('error-message');

        loading.classList.add('hidden');
        mainContainer.classList.add('hidden');
        selectScreen.classList.add('hidden');
        errorScreen.classList.remove('hidden');
        errorMessage.textContent = message;
    }

    showLoading() {
        const loading = document.getElementById('loading');
        const mainContainer = document.getElementById('main-container');
        const selectScreen = document.getElementById('select-screen');
        const errorScreen = document.getElementById('error-screen');

        loading.classList.remove('hidden');
        mainContainer.classList.add('hidden');
        selectScreen.classList.add('hidden');
        errorScreen.classList.add('hidden');
    }

    showMain() {
        const loading = document.getElementById('loading');
        const mainContainer = document.getElementById('main-container');
        const selectScreen = document.getElementById('select-screen');
        const errorScreen = document.getElementById('error-screen');

        loading.classList.add('hidden');
        mainContainer.classList.remove('hidden');
        selectScreen.classList.add('hidden');
        errorScreen.classList.add('hidden');
    }

    showSelectScreen() {
        const loading = document.getElementById('loading');
        const mainContainer = document.getElementById('main-container');
        const selectScreen = document.getElementById('select-screen');
        const errorScreen = document.getElementById('error-screen');

        loading.classList.add('hidden');
        mainContainer.classList.add('hidden');
        selectScreen.classList.remove('hidden');
        errorScreen.classList.add('hidden');
    }

    reset() {
        this.currentDialogueIndex = -1;
        this.isShowingChoices = false;
        this.isEnded = false;
        this.isTextAnimating = false;
        this.isPaused = false;
        this.startIndex = 0;
        this.clearTypewriter();

        document.getElementById('end-screen').classList.add('hidden');
        document.getElementById('choice-panel').classList.add('hidden');
        document.getElementById('dialogue-box').classList.add('hidden');

        if (this.project && this.project.scenes && this.project.scenes.length > 0) {
            this.goToScene(0);
        }
    }
}

window.Renderer = Renderer;
