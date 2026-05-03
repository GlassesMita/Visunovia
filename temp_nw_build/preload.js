if (typeof window !== 'undefined') {
    window.addEventListener('message', (event) => {
        const { channel, data } = event.data || {};

        if (!channel) return;

        switch (channel) {
            case 'project:loaded':
                if (window.onProjectLoaded) {
                    window.onProjectLoaded(data);
                }
                break;
            case 'project:error':
                if (window.onProjectError) {
                    window.onProjectError(data);
                }
                break;
            case 'project:auto-loaded':
                if (window.onProjectAutoLoaded) {
                    window.onProjectAutoLoaded(data);
                }
                break;
            case 'player:restart':
                if (window.onPlayerRestart) {
                    window.onPlayerRestart();
                }
                break;
            case 'player:toggle':
                if (window.onPlayerToggle) {
                    window.onPlayerToggle();
                }
                break;
        }
    });

    window.playerAPI = {
        openProjectDialog: () => {
            const input = document.createElement('input');
            input.type = 'file';
            input.accept = '.zip,.tlor,.lor';
            input.onchange = (e) => {
                const file = e.target.files[0];
                if (file && window.confirm('请输入项目密码')) {
                    const password = prompt('请输入项目密码:');
                    if (password) {
                        window.postMessage({
                            type: 'command',
                            command: 'mount-project',
                            projectPath: file.path,
                            password: password
                        }, '*');
                    }
                }
            };
            input.click();
        },

        minimize: () => {
            window.postMessage({ type: 'command', command: 'window:minimize' }, '*');
        },

        maximize: () => {
            window.postMessage({ type: 'command', command: 'window:maximize' }, '*');
        },

        close: () => {
            window.postMessage({ type: 'command', command: 'window:close' }, '*');
        },

        openDevTools: () => {
            window.postMessage({ type: 'command', command: 'window:open-devtools' }, '*');
        },

        onProjectLoaded: (callback) => {
            window.onProjectLoaded = callback;
        },

        onProjectError: (callback) => {
            window.onProjectError = callback;
        },

        onProjectAutoLoaded: (callback) => {
            window.onProjectAutoLoaded = callback;
        },

        onPlayerRestart: (callback) => {
            window.onPlayerRestart = callback;
        },

        onPlayerToggle: (callback) => {
            window.onPlayerToggle = callback;
        }
    };
}
