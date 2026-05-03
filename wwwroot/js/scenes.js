var app = app || {};

app.updateSceneTabs = function () {
    var container = document.getElementById('sceneTabs');
    if (!container) return;

    container.innerHTML = '';

    if (!app.state.project || !app.state.project.scenes) {
        var addBtn = document.createElement('button');
        addBtn.className = 'add-scene-btn';
        addBtn.textContent = '+ 添加场景';
        addBtn.onclick = function () { app.addScene(); };
        container.appendChild(addBtn);
        return;
    }

    var scenes = app.state.project.scenes;
    for (var i = 0; i < scenes.length; i++) {
        (function (index) {
            var tab = document.createElement('div');
            tab.className = 'scene-tab';
            if (index === app.state.activeSceneIndex) {
                tab.classList.add('active');
            }
            tab.textContent = scenes[index].id || '场景 ' + (index + 1);

            tab.onclick = function () {
                app.selectScene(index);
            };

            tab.ondblclick = function () {
                app.renameScene(index);
            };

            tab.oncontextmenu = function (e) {
                e.preventDefault();
                if (app.state.project.scenes.length > 1) {
                    app.deleteScene(index);
                } else {
                    app.setStatus('至少保留一个场景');
                }
            };

            container.appendChild(tab);
        })(i);
    }

    var addBtn = document.createElement('button');
    addBtn.className = 'add-scene-btn';
    addBtn.textContent = '+ 添加场景';
    addBtn.onclick = function () { app.addScene(); };
    container.appendChild(addBtn);
};
