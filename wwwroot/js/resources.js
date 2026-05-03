var app = app || {};

app.updateResourceList = function () {
    var container = document.getElementById('resourceList');
    if (!container) return;

    if (!app.state.project) {
        container.innerHTML = '<p style="color: #666; font-size: 12px; padding: 8px;">打开项目后显示资源</p>';
        return;
    }

    fetch('/api/resources', { method: 'GET' })
        .then(function (res) { return res.json(); })
        .then(function (data) {
            app.state.cachedResources = data;
            var html = '';

            var categories = [
                { key: 'sprites', label: '立绘', icon: '🎭' },
                { key: 'backgrounds', label: '背景', icon: '🖼️' },
                { key: 'bgm', label: '音乐', icon: '🎵' },
                { key: 'voice', label: '语音', icon: '🎤' },
                { key: 'sfx', label: '音效', icon: '🔊' }
            ];

            for (var i = 0; i < categories.length; i++) {
                var cat = categories[i];
                var items = data[cat.key] || [];
                html += '<div style="margin-bottom:8px;">';
                html += '<div style="color:#999;font-size:11px;font-weight:bold;margin-bottom:2px;">' + cat.icon + ' ' + cat.label + ' (' + items.length + ')</div>';
                if (items.length > 0) {
                    for (var j = 0; j < items.length; j++) {
                        html += '<div style="color:#666;font-size:11px;padding:1px 8px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;" title="' + app.escapeAttr(items[j]) + '">' + app.escapeHtml(items[j]) + '</div>';
                    }
                } else {
                    html += '<div style="color:#444;font-size:11px;padding:1px 8px;">(空)</div>';
                }
                html += '</div>';
            }

            container.innerHTML = html;
        })
        .catch(function () {
            container.innerHTML = '<p style="color: #666; font-size: 12px; padding: 8px;">加载资源失败</p>';
        });
};

app.renderModalResources = function (data) {
    var listEl = document.getElementById('modalResourceList');
    if (!listEl) return;

    listEl.innerHTML = '';

    var tab = app.state.currentResourceTab || 'sprites';
    var items = data[tab] || [];

    if (items.length === 0) {
        listEl.innerHTML = '<li style="color:#666;padding:8px;text-align:center;">此分类没有资源</li>';
        return;
    }

    for (var i = 0; i < items.length; i++) {
        (function (item) {
            var li = document.createElement('li');
            li.style.cssText = 'padding:6px 12px;cursor:pointer;border-bottom:1px solid #333;color:#ccc;font-size:13px;display:flex;align-items:center;gap:8px;';

            if (app.state.selectedResourceItem === item) {
                li.style.background = '#3B82F6';
                li.style.color = 'white';
            }

            var isImage = tab === 'sprites' || tab === 'backgrounds';
            if (isImage) {
                var img = document.createElement('img');
                img.style.cssText = 'width:32px;height:32px;object-fit:cover;border-radius:2px;flex-shrink:0;';
                img.src = '/api/resources/file/Assets/' + app.getResourceSubDir(tab) + '/' + item;
                img.onerror = function () { this.style.display = 'none'; };
                li.appendChild(img);
            }

            var span = document.createElement('span');
            span.textContent = item;
            span.style.cssText = 'overflow:hidden;text-overflow:ellipsis;white-space:nowrap;';
            li.appendChild(span);

            li.onclick = function () {
                app.state.selectedResourceItem = item;
                app.renderModalResources(data);
            };

            listEl.appendChild(li);
        })(items[i]);
    }
};

app.getResourceSubDir = function (tab) {
    switch (tab) {
        case 'sprites': return 'Characters';
        case 'backgrounds': return 'Backgrounds';
        case 'bgm': return 'Musics';
        case 'voice': return 'Voices';
        case 'sfx': return 'Sfx';
        default: return '';
    }
};
