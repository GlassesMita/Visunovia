let renderer = null;

document.addEventListener('DOMContentLoaded', async () => {
    renderer = new Renderer();

    const mainContainer = document.getElementById('main-container');
    const dialogueBox = document.getElementById('dialogue-box');
    const closeBtn = document.getElementById('close-btn');
    const replayBtn = document.getElementById('replay-btn');
    const retryBtn = document.getElementById('retry-btn');
    const openProjectBtn = document.getElementById('open-project-btn');

    if (window.playerAPI) {
        setupPlayerAPIListeners();
    } else {
        setupLocalModeListeners();
    }

    dialogueBox.addEventListener('click', () => {
        if (!renderer || renderer.isShowingChoices || renderer.isEnded) return;

        if (renderer.isTextAnimating) {
            renderer.skipTextAnimation();
        } else {
            renderer.advance();
        }
    });

    mainContainer.addEventListener('click', (e) => {
        if (e.target === mainContainer || e.target.id === 'background-image') {
            if (!renderer || renderer.isShowingChoices || renderer.isEnded) return;

            if (renderer.isTextAnimating) {
                renderer.skipTextAnimation();
            } else {
                renderer.advance();
            }
        }
    });

    closeBtn?.addEventListener('click', () => {
        if (window.playerAPI) {
            window.playerAPI.close();
        } else {
            window.close();
        }
    });

    replayBtn?.addEventListener('click', () => {
        if (renderer) {
            renderer.reset();
        }
    });

    retryBtn?.addEventListener('click', async () => {
        if (window.playerAPI) {
            renderer.showSelectScreen();
        } else {
            location.reload();
        }
    });

    openProjectBtn?.addEventListener('click', () => {
        if (window.playerAPI) {
            window.playerAPI.openProjectDialog();
        }
    });

    document.addEventListener('keydown', (e) => {
        if (!renderer || renderer.isShowingChoices || renderer.isEnded) return;

        if (e.key === ' ' || e.key === 'Enter') {
            if (renderer.isTextAnimating) {
                renderer.skipTextAnimation();
            } else {
                renderer.advance();
            }
        }
    });
});

function setupPlayerAPIListeners() {
    window.playerAPI.onProjectLoaded((data) => {
        if (!renderer) return;

        try {
            const projectData = data.projectData;
            const scripts = data.scripts || {};
            const assets = data.assets;

            if (!projectData || !projectData.scenes || projectData.scenes.length === 0) {
                renderer.showError('项目中没有场景数据');
                return;
            }

            renderer.loadProject(projectData, scripts, assets);
            renderer.showMain();
        } catch (error) {
            renderer.showError(error.message || '加载项目失败');
        }
    });

    window.playerAPI.onProjectError((data) => {
        if (!renderer) return;
        renderer.showError(data.message || '加载项目失败');
    });

    window.playerAPI.onProjectAutoLoaded((data) => {
        if (!renderer) return;

        try {
            const projectData = data.projectData;
            if (projectData) {
                renderer.loadProject(projectData);
                renderer.showMain();
            }
        } catch (error) {
            console.error('自动加载项目失败:', error);
        }
    });

    window.playerAPI.onPlayerRestart(() => {
        if (renderer) {
            renderer.reset();
        }
    });

    window.playerAPI.onPlayerToggle(() => {
        if (renderer) {
            renderer.togglePause();
        }
    });
}

function setupLocalModeListeners() {
    renderer.showLoading();

    fetch('/api/project')
        .then(response => {
            if (!response.ok) {
                throw new Error('无法从服务器获取项目数据');
            }
            return response.json();
        })
        .then(data => {
            const projectData = JSON.parse(data.projectJson);

            if (!projectData || !projectData.scenes || projectData.scenes.length === 0) {
                throw new Error('无法加载项目数据');
            }

            renderer.loadProject({
                ...projectData,
                startIndex: data.startIndex || 0
            });
            renderer.showMain();
        })
        .catch(error => {
            console.error('加载失败:', error);
            renderer.showError(error.message || '加载项目失败');
        });
}
