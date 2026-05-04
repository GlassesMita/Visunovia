var app = app || {};

app.state = {
    activeSceneIndex: 0,
    selectedDialogueIndex: -1,
    project: null,
    projectPath: '',
    hasUnsavedChanges: false,
    renamingSceneIndex: -1,
    currentResourceTab: 'sprites',
    selectedResourceItem: null,
    resourceTargetField: null,
    cachedResources: null,
    fileBrowserSelectedPath: null,
    fileBrowserSelectedType: null,
    fileBrowserCurrentPath: null,
    fileBrowserParentPath: null
};

app.fetchJson = function (url, options) {
    return fetch(url, options)
        .then(function (response) {
            if (!response.ok) {
                return response.json().then(function (err) {
                    throw new Error(err.error || 'HTTP ' + response.status + ': ' + response.statusText);
                }).catch(function () {
                    throw new Error('HTTP ' + response.status + ': ' + response.statusText);
                });
            }
            return response.json();
        });
};

app.fetchText = function (url, options) {
    options = options || {};
    options.headers = options.headers || {};
    return fetch(url, options).then(function (response) {
        if (!response.ok) {
            throw new Error('HTTP ' + response.status);
        }
        return response.text();
    });
};

app.showLoading = function (message) {
    var overlay = document.getElementById('loadingOverlay');
    if (overlay) {
        overlay.style.display = 'flex';
        if (message) {
            overlay.querySelector('div:last-child').textContent = message;
        }
    }
};

app.hideLoading = function () {
    var overlay = document.getElementById('loadingOverlay');
    if (overlay) {
        overlay.style.display = 'none';
    }
};

app.parseProjectXml = function (xmlString) {
    var parser = new DOMParser();
    var doc = parser.parseFromString(xmlString, 'application/xml');

    var parseError = doc.querySelector('parsererror');
    if (parseError) {
        console.error('XML Parse Error:', parseError.textContent);
        app.hideLoading();
        app.setStatus('XML 解析错误');
        return null;
    }

    var projectEl = doc.documentElement;

    var metadata = {};
    var metaEl = projectEl.querySelector('metadata');
    if (metaEl) {
        metadata.title = (metaEl.querySelector('title') || {}).textContent || '';
        metadata.author = (metaEl.querySelector('author') || {}).textContent || '';
        metadata.version = (metaEl.querySelector('version') || {}).textContent || '1.0';
    }

    var scenes = [];
    var sceneEls = projectEl.querySelectorAll('scene');
    sceneEls.forEach(function (sceneEl) {
        var scene = {
            id: sceneEl.getAttribute('id') || '',
            background: (sceneEl.querySelector('background') || {}).textContent || '',
            bgm: {},
            dialogues: []
        };

        var bgmEl = sceneEl.querySelector('bgm');
        if (bgmEl) {
            scene.bgm.path = (bgmEl.querySelector('path') || {}).textContent || '';
            scene.bgm.volume = parseInt((bgmEl.querySelector('volume') || {}).textContent) || 80;
            scene.bgm.loop = ((bgmEl.querySelector('loop') || {}).textContent || 'true') === 'true';
        }

        var dialogueEls = sceneEl.querySelectorAll('dialogue');
        dialogueEls.forEach(function (dialogueEl) {
            var type = dialogueEl.getAttribute('type') || 'Dialogue';
            var dialogue = { type: type };

            if (type === 'Dialogue') {
                dialogue.speaker = (dialogueEl.querySelector('speaker') || {}).textContent || '';
                dialogue.text = (dialogueEl.querySelector('text') || {}).textContent || '';
                dialogue.background = (dialogueEl.querySelector('background') || {}).textContent || '';
                dialogue.voice = (dialogueEl.querySelector('voice') || {}).textContent || '';

                dialogue.sprites = [];
                var spriteEls = dialogueEl.querySelectorAll('sprites sprite');
                spriteEls.forEach(function (spriteEl) {
                    var animEl = spriteEl.querySelector('animation');
                    dialogue.sprites.push({
                        path: (spriteEl.querySelector('path') || {}).textContent || '',
                        position: (spriteEl.querySelector('position') || {}).textContent || 'center',
                        layer: parseInt((spriteEl.querySelector('layer') || {}).textContent) || 0,
                        animation: {
                            type: (animEl || {}).getAttribute('type') || 'none',
                            duration: parseInt((animEl || {}).getAttribute('duration') || '300')
                        }
                    });
                });

                var teEl = dialogueEl.querySelector('textEffect');
                if (teEl) {
                    dialogue.textEffect = {
                        type: teEl.getAttribute('type') || 'None',
                        speed: parseInt(teEl.getAttribute('speed') || '50'),
                        shake: teEl.getAttribute('shake') === 'true',
                        fadeDuration: parseInt(teEl.getAttribute('fadeDuration') || '500'),
                        delayBeforeStart: parseInt(teEl.getAttribute('delayBeforeStart') || '0')
                    };
                }

                var animEl = dialogueEl.querySelector(':scope > animation');
                if (animEl) {
                    dialogue.animation = {
                        type: animEl.getAttribute('type') || 'none',
                        duration: parseInt(animEl.getAttribute('duration') || '300')
                    };
                }

                var transEl = dialogueEl.querySelector(':scope > transition');
                if (transEl) {
                    dialogue.transition = {
                        type: transEl.getAttribute('type') || 'None',
                        duration: parseInt(transEl.getAttribute('duration') || '300')
                    };
                }
            } else if (type === 'Branch') {
                var branchEl = dialogueEl.querySelector('branch');
                if (branchEl) {
                    dialogue.branch = { choices: [] };
                    var choiceEls = branchEl.querySelectorAll('choice');
                    choiceEls.forEach(function (choiceEl) {
                        dialogue.branch.choices.push({
                            text: choiceEl.getAttribute('text') || '',
                            targetScene: choiceEl.getAttribute('targetScene') || '',
                            condition: choiceEl.getAttribute('condition') || ''
                        });
                    });
                }
            } else if (type === 'Event') {
                var eventEl = dialogueEl.querySelector('event');
                if (eventEl) {
                    dialogue.event = {
                        eventType: eventEl.getAttribute('eventType') || 'Custom',
                        parameters: {}
                    };
                    var paramEls = eventEl.querySelectorAll('parameters > *');
                    var paramNameMap = {
                        'BackgroundPath': 'background',
                        'BgmPath': 'bgmFile',
                        'CharacterPath': 'character',
                        'TargetScene': 'targetScene',
                        'SoundPath': 'soundFile',
                        'VariableName': 'variableName',
                        'VariableValue': 'variableValue',
                        'Duration': 'duration',
                        'Command': 'command',
                        // 自引用映射：后端序列化时使用前端 camelCase key 作为 XML 元素名，
                        // 重新读取时需直接匹配，防止 toLowerCase() 破坏驼峰命名（如 bgmFile → bgmfile）
                        'bgmFile': 'bgmFile',
                        'soundFile': 'soundFile',
                        'background': 'background',
                        'character': 'character',
                        'targetScene': 'targetScene',
                        'variableName': 'variableName',
                        'variableValue': 'variableValue',
                        'duration': 'duration',
                        'command': 'command'
                    };
                    paramEls.forEach(function (paramEl) {
                        var xmlName = paramEl.nodeName;
                        var mappedName = paramNameMap[xmlName] || xmlName.toLowerCase();
                        // DOMParser 自动解码一层 XML 实体（如 &amp; → &），但 &apos; 等仍可能残留
                        // 对参数值进行循环解码，处理历史项目中累积的多重编码数据
                        var rawValue = paramEl.textContent || '';
                        var decodedValue = rawValue;
                        var prevVal;
                        var maxDecodeIter = 10;
                        do {
                            prevVal = decodedValue;
                            decodedValue = decodedValue
                                .replace(/&apos;/g, "'")
                                .replace(/&quot;/g, '"')
                                .replace(/&amp;/g, '&')
                                .replace(/&lt;/g, '<')
                                .replace(/&gt;/g, '>');
                            maxDecodeIter--;
                        } while (decodedValue !== prevVal && maxDecodeIter > 0);
                        dialogue.event.parameters[mappedName] = decodedValue;
                    });
                }

                var transEl = dialogueEl.querySelector(':scope > transition');
                if (transEl) {
                    dialogue.transition = {
                        type: transEl.getAttribute('type') || 'None',
                        duration: parseInt(transEl.getAttribute('duration') || '300')
                    };
                }
            }

            scene.dialogues.push(dialogue);
        });

        scenes.push(scene);
    });

    return {
        metadata: metadata,
        scenes: scenes,
        variables: []
    };
};

app.serializeProjectToXml = function () {
    var project = app.state.project;
    if (!project) return '';

    var escapeXml = function (str) {
        if (!str) return '';
        return String(str).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&apos;');
    };

    var xml = '<?xml version="1.0" encoding="utf-8"?>\r\n';
    xml += '<project version="1.0">\r\n';

    xml += '  <metadata>\r\n';
    xml += '    <title>' + escapeXml(project.metadata.title) + '</title>\r\n';
    xml += '    <author>' + escapeXml(project.metadata.author || '') + '</author>\r\n';
    xml += '    <version>' + escapeXml(project.metadata.version || '1.0') + '</version>\r\n';
    xml += '  </metadata>\r\n';

    xml += '  <scenes>\r\n';
    project.scenes.forEach(function (scene) {
        xml += '    <scene id="' + escapeXml(scene.id) + '">\r\n';
        xml += '      <background>' + escapeXml(scene.background || '') + '</background>\r\n';

        xml += '      <bgm>\r\n';
        xml += '        <path>' + escapeXml((scene.bgm && scene.bgm.path) || '') + '</path>\r\n';
        xml += '        <volume>' + ((scene.bgm && scene.bgm.volume) || 80) + '</volume>\r\n';
        xml += '        <loop>' + (((scene.bgm && scene.bgm.loop) !== false) ? 'true' : 'false') + '</loop>\r\n';
        xml += '      </bgm>\r\n';

        xml += '      <dialogues>\r\n';
        (scene.dialogues || []).forEach(function (d) {
            xml += '        <dialogue type="' + d.type + '">\r\n';

            if (d.type === 'Dialogue') {
                xml += '          <speaker>' + escapeXml(d.speaker || '') + '</speaker>\r\n';
                xml += '          <text>' + escapeXml(d.text || '') + '</text>\r\n';
                xml += '          <background>' + escapeXml(d.background || '') + '</background>\r\n';

                xml += '          <sprites>\r\n';
                (d.sprites || []).forEach(function (s) {
                    xml += '            <sprite>\r\n';
                    xml += '              <path>' + escapeXml(s.path || '') + '</path>\r\n';
                    xml += '              <position>' + escapeXml(s.position || 'center') + '</position>\r\n';
                    xml += '              <layer>' + (s.layer || 0) + '</layer>\r\n';
                    var animType = (s.animation && s.animation.type) ? s.animation.type : 'none';
                    var animDur = (s.animation && s.animation.duration) ? s.animation.duration : 300;
                    xml += '              <animation type="' + animType + '" duration="' + animDur + '"/>\r\n';
                    xml += '            </sprite>\r\n';
                });
                xml += '          </sprites>\r\n';

                xml += '          <voice>' + escapeXml(d.voice || '') + '</voice>\r\n';

                var te = d.textEffect || {};
                xml += '          <textEffect type="' + (te.type || 'None') + '" speed="' + (te.speed || 50) + '" shake="' + (te.shake ? 'true' : 'false') + '" fadeDuration="' + (te.fadeDuration || 500) + '" delayBeforeStart="' + (te.delayBeforeStart || 0) + '"/>\r\n';

                var da = d.animation || {};
                xml += '          <animation type="' + (da.type || 'none') + '" duration="' + (da.duration || 300) + '"/>\r\n';

                var dt = d.transition || {};
                xml += '          <transition type="' + (dt.type || 'None') + '" duration="' + (dt.duration || 300) + '"/>\r\n';
            } else if (d.type === 'Branch') {
                xml += '          <branch>\r\n';
                xml += '            <choices>\r\n';
                (d.branch && d.branch.choices || []).forEach(function (c) {
                    xml += '              <choice text="' + escapeXml(c.text || '') + '" targetScene="' + escapeXml(c.targetScene || '') + '" condition="' + escapeXml(c.condition || '') + '"/>\r\n';
                });
                xml += '            </choices>\r\n';
                xml += '          </branch>\r\n';
            } else if (d.type === 'Event') {
                var ev = d.event || {};
                xml += '          <event eventType="' + ev.eventType + '">\r\n';
                xml += '            <parameters>\r\n';
                var params = ev.parameters || {};
                for (var pk in params) {
                    if (Object.prototype.hasOwnProperty.call(params, pk)) {
                        console.log('[SAVE-DEBUG] 序列化参数: key=' + pk + ' rawValue=[' + params[pk] + '] escaped=[' + escapeXml(params[pk]) + ']');
                        xml += '              <' + pk + '>' + escapeXml(params[pk]) + '</' + pk + '>\r\n';
                    }
                }
                xml += '            </parameters>\r\n';
                xml += '          </event>\r\n';

                var evt = d.transition || {};
                xml += '          <transition type="' + (evt.type || 'None') + '" duration="' + (evt.duration || 300) + '"/>\r\n';
            }

            xml += '        </dialogue>\r\n';
        });
        xml += '      </dialogues>\r\n';
        xml += '    </scene>\r\n';
    });
    xml += '  </scenes>\r\n';

    xml += '  <variables/>\r\n';
    xml += '</project>';

    return xml;
};

app.setStatus = function (text) {
    var el = document.getElementById('statusLabel');
    if (el) {
        el.textContent = text;
    }
};

app.setSaveStatus = function (text) {
    var el = document.getElementById('saveStatus');
    if (el) {
        el.textContent = text;
    }
};

app.updateProjectFromResponse = function (data) {
    app.state.project = app.normalizeProjectData(data);
    app.state.hasUnsavedChanges = data.hasUnsavedChanges || false;
    app.state.projectPath = data.projectPath || '';
    app.setSaveStatus('未保存');
};

app.normalizeProjectData = function (data) {
    if (!data || typeof data !== 'object') return data;
    var normalized = {};
    var keyMap = {
        'Scenes': 'scenes', 'Dialogues': 'dialogues', 'Metadata': 'metadata',
        'Variables': 'variables', 'ProjectPath': 'projectPath',
        'HasUnsavedChanges': 'hasUnsavedChanges', 'ActiveSceneIndex': 'activeSceneIndex',
        'SelectedDialogueIndex': 'selectedDialogueIndex', 'Speaker': 'speaker',
        'Text': 'text', 'Type': 'type', 'Background': 'background',
        'Sprites': 'sprites', 'Voice': 'voice', 'Branch': 'branch',
        'Event': 'event', 'Choices': 'choices', 'Id': 'id',
        'Bgm': 'bgm', 'EventType': 'eventType', 'Parameters': 'parameters',
        'Title': 'title', 'Author': 'author', 'Version': 'version'
    };
    var typeMap = { 0: 'Dialogue', 1: 'Branch', 2: 'Event' };
    var eventTypeMap = {
        0: 'JumpScene', 1: 'SetVariable', 2: 'PlaySound',
        3: 'ChangeBackground', 4: 'ChangeBgm', 5: 'ShowCharacter',
        6: 'HideCharacter', 7: 'Pause', 8: 'WaitSeconds',
        9: 'WindowEffect', 10: 'Custom'
    };
    for (var key in data) {
        if (!Object.prototype.hasOwnProperty.call(data, key)) continue;
        var newKey = keyMap[key] || key;
        var value = data[key];
        if (Array.isArray(value)) {
            normalized[newKey] = value.map(function (item) { return app.normalizeProjectData(item); });
        } else if (value !== null && typeof value === 'object') {
            normalized[newKey] = app.normalizeProjectData(value);
        } else {
            normalized[newKey] = value;
        }
    }
    if (normalized.type !== undefined && typeMap[normalized.type]) {
        normalized.type = typeMap[normalized.type];
    }
    if (normalized.eventType !== undefined && eventTypeMap[normalized.eventType]) {
        normalized.eventType = eventTypeMap[normalized.eventType];
    }
    return normalized;
};

app.newProject = function () {
    app.showLoading('正在创建项目...');
    app.fetchText('/api/project/new', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name: '未命名项目', path: '' })
    })
    .then(function (xmlString) {
        var data = app.parseProjectXml(xmlString);
        if (data) {
            app.state.project = data;
            app.state.activeSceneIndex = 0;
            app.state.selectedDialogueIndex = -1;
            app.state.hasUnsavedChanges = true;
            app.state.projectPath = '';
            app.setStatus('新项目已创建');
            app.setSaveStatus('未保存');
            app.updateUI();
        }
    })
    .catch(function (err) {
        app.setStatus('创建项目失败: ' + err.message);
    })
    .finally(function () {
        app.hideLoading();
    });
};

app.openProject = function () {
    app.showFileBrowser();
};

app.closeFileBrowser = function () {
    document.getElementById('fileBrowserModal').style.display = 'none';
};

app.showFileBrowser = function () {
    var modal = document.getElementById('fileBrowserModal');
    modal.style.display = 'block';
    app.fileBrowserSelectedPath = null;

    var pathInput = document.getElementById('fileBrowserPath');
    if (pathInput) {
        // 移除旧监听器，避免重复绑定
        pathInput.removeEventListener('keydown', app._fbPathKeyHandler);
        // 绑定 Enter/Escape 键盘事件：Enter 导航到输入路径，Escape 恢复原始路径
        app._fbPathKeyHandler = function (e) {
            if (e.key === 'Enter') {
                e.preventDefault();
                app._fbUserTyping = false; // 清除输入标记，允许响应更新路径显示
                var newPath = this.value.trim();
                if (newPath) {
                    app.loadFileBrowserDirectory(newPath);
                }
            } else if (e.key === 'Escape') {
                e.preventDefault();
                this.value = app.fileBrowserCurrentPath || '';
                this.blur();
            }
        };
        pathInput.addEventListener('keydown', app._fbPathKeyHandler);
        // 聚焦时全选文本，方便用户直接输入新路径
        pathInput.addEventListener('focus', function () { this.select(); });
        // 标记用户正在手动输入，防止异步 API 响应覆盖输入内容
        if (!app._fbInputHandler) {
            app._fbInputHandler = function () { app._fbUserTyping = true; };
        }
        pathInput.removeEventListener('input', app._fbInputHandler);
        pathInput.addEventListener('input', app._fbInputHandler);
    }

    app.loadFileBrowserDirectory();
};

app.loadFileBrowserDirectory = function (path) {
        var contentDiv = document.getElementById('fileBrowserContent');
        var pathDiv = document.getElementById('fileBrowserPath');
        contentDiv.innerHTML = '<div style="text-align:center; color:#666; padding:40px;">加载中...</div>';
        // 仅在非手动输入模式下更新路径显示（避免覆盖用户正在输入的内容）
        if (!app._fbUserTyping) {
            pathDiv.value = path || '加载中...';
        }

        var url = '/api/files/browse';
        if (path) {
            var encodedPath = path.replace(/\\/g, '/');
            url += '?path=' + encodeURIComponent(encodedPath);
        }

        fetch(url)
        .then(function (response) {
            if (!response.ok) throw new Error('HTTP ' + response.status);
            return response.json();
        })
        .then(function (data) {
            // 同样检查：仅在非手动输入模式时才用响应数据更新输入框
            if (!app._fbUserTyping && pathDiv) {
                pathDiv.value = data.path;
            }
        app.fileBrowserCurrentPath = data.path;
        app.fileBrowserParentPath = data.parent;

        var html = '';
        if (data.parent) {
            html += '<div class="fb-item" data-path="' + app.escapeAttr(data.parent) + '" data-type="parent" style="color:#888; cursor:pointer; padding:4px 8px;">📁 ..</div>';
        }

        if (!data.entries || data.entries.length === 0) {
            html += '<div style="text-align:center; color:#666; padding:40px;">目录为空</div>';
        } else {
        for (var i = 0; i < data.entries.length; i++) {
            var entry = data.entries[i];
            var isTlor = entry.extension === '.tlor';
            if (entry.type === 'directory') {
                html += '<div class="fb-item" data-path="' + app.escapeAttr(entry.path) + '" data-type="directory" style="cursor:pointer; padding:4px 8px; color:#fff;">📁 ' + app.escapeHtml(entry.name) + '</div>';
            } else if (isTlor) {
                var sizeStr = app.formatFileSize(entry.size);
                html += '<div class="fb-item fb-tlor" data-path="' + app.escapeAttr(entry.path) + '" data-type="file" data-ext=".tlor" style="cursor:pointer; padding:4px 8px; color:#3B82F6;">📋 ' + app.escapeHtml(entry.name) + '<span style="float:right;color:#666;font-size:11px;margin-right:4px;">' + sizeStr + '</span></div>';
            } else {
                var sizeStr2 = app.formatFileSize(entry.size);
                html += '<div class="fb-item fb-disabled" data-path="' + app.escapeAttr(entry.path) + '" data-type="file" data-ext="' + app.escapeAttr(entry.extension) + '" style="cursor:not-allowed; padding:4px 8px; color:#555;">📄 ' + app.escapeHtml(entry.name) + '<span style="float:right;color:#444;font-size:11px;margin-right:4px;">' + sizeStr2 + '</span></div>';
            }
        }
        }
        contentDiv.innerHTML = html;

        contentDiv.querySelectorAll('.fb-item').forEach(function(item) {
            item.addEventListener('click', function() {
                var itemPath = this.getAttribute('data-path');
                var itemType = this.getAttribute('data-type');
                app.handleFileBrowserClick(itemPath, itemType);
            });
        });
    })
    .catch(function (err) {
        contentDiv.innerHTML = '<div style="color:#f55; padding:40px;">加载失败: ' + err.message + '</div>';
    });
};

app.handleFileBrowserClick = function (path, type) {
    if (type === 'parent') {
        app.loadFileBrowserDirectory(path);
        return;
    }

    if (type === 'directory') {
        app.loadFileBrowserDirectory(path);
        return;
    }

    var isTlor = event.target.closest('[data-ext]')?.getAttribute('data-ext') === '.tlor';
    if (!isTlor) {
        return;
    }

    app.fileBrowserSelectedPath = path;
    app.fileBrowserSelectedType = type;

    var items = document.querySelectorAll('.fb-item');
    for (var i = 0; i < items.length; i++) {
        items[i].style.background = '';
    }
    event.target.style.background = '#3B82F6';
};

app.escapeAttr = function (str) {
    if (!str) return '';
    return str.replace(/"/g, '&quot;');
};

app.formatFileSize = function (bytes) {
    if (bytes === undefined || bytes === null || bytes === 0) return '0 B';
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1).replace(/\.0$/, '') + ' KB';
    return (bytes / (1024 * 1024)).toFixed(1).replace(/\.0$/, '') + ' MB';
};

app.openSelectedInFileBrowser = function () {
    if (!app.fileBrowserSelectedPath) {
        app.setStatus('请先选择要打开的项目');
        return;
    }

    if (app.fileBrowserSelectedType === 'directory') {
        app.loadFileBrowserDirectory(app.fileBrowserSelectedPath);
        return;
    }

    app.closeFileBrowser();
    app.showLoading('正在打开项目...');

    fetch('/api/files/openProject', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ path: app.fileBrowserSelectedPath })
    })
    .then(function (response) {
        if (!response.ok) throw new Error('HTTP ' + response.status);
        return response.text();
    })
    .then(function (xmlString) {
        var data = app.parseProjectXml(xmlString);
        if (data) {
            app.state.project = data;
            app.state.activeSceneIndex = 0;
            app.state.selectedDialogueIndex = -1;
            app.state.hasUnsavedChanges = false;
            app.state.projectPath = app.fileBrowserSelectedPath;
            app.setStatus('项目已打开: ' + (data.metadata ? data.metadata.title : '项目'));
            app.setSaveStatus('已保存');
            app.updateUI();

            // 更新 URL 参数，使页面刷新后能自动恢复项目
            var newUrl = window.location.pathname + '?project=' + encodeURIComponent(app.fileBrowserSelectedPath);
            window.history.replaceState({ path: app.fileBrowserSelectedPath }, '', newUrl);
            console.log('[DEBUG] URL 已更新，包含项目路径参数');
        }
    })
    .catch(function (err) {
        app.setStatus('打开项目失败: ' + err.message);
    })
    .finally(function () {
        app.hideLoading();
    });
};

app.escapeHtml = function (str) {
    if (!str) return '';
    return str.replace(/\\/g, '\\\\').replace(/'/g, "\\'");
};

app.handleProjectUpload = function (event) {
    var files = event.target.files;
    console.log('[DEBUG] handleProjectUpload 触发，选择了', files.length, '个文件/文件夹项');
    if (!files || files.length === 0) {
        console.log('[DEBUG] 没有选择文件');
        return;
    }

    var tlorFile = null;
    var projectFolder = null;

    for (var i = 0; i < files.length; i++) {
        var file = files[i];
        var fullPath = file.webkitRelativePath || file.name;
        console.log('[DEBUG] 文件路径:', fullPath);
        if (fullPath.endsWith('Project.tlor')) {
            tlorFile = file;
            projectFolder = fullPath.substring(0, fullPath.length - 'Project.tlor'.length);
            console.log('[DEBUG] 找到 Project.tlor，项目文件夹:', projectFolder);
            break;
        }
    }

    if (!tlorFile) {
        console.log('[DEBUG] 未找到 Project.tlor 文件');
        app.setStatus('请选择包含 Project.tlor 的项目文件夹');
        app.hideLoading();
        return;
    }

    app.showLoading('正在打开项目...');
    console.log('[DEBUG] 开始上传文件:', tlorFile.name);
    var formData = new FormData();
    formData.append('uploadedFile', tlorFile);
    formData.append('projectPath', projectFolder || '');

    console.log('[DEBUG] FormData 创建完成，projectPath:', projectFolder);
    app.fetchText('/api/project/upload?projectPath=' + encodeURIComponent(projectFolder || ''), {
        method: 'POST',
        body: formData
    })
    .then(function (xmlString) {
        console.log('[DEBUG] 收到响应，长度:', xmlString ? xmlString.length : 0);
        var data = app.parseProjectXml(xmlString);
        if (data) {
            app.state.project = data;
            app.state.activeSceneIndex = 0;
            app.state.selectedDialogueIndex = -1;
            app.state.hasUnsavedChanges = false;
            app.state.projectPath = projectFolder;
            app.setStatus('项目已打开: ' + (data.metadata ? data.metadata.title : tlorFile.name));
            app.setSaveStatus('已保存');
            app.updateUI();
            console.log('[DEBUG] 项目已加载到状态中');

            // 更新 URL 参数，使页面刷新后能自动恢复项目
            if (projectFolder) {
                var newUrl = window.location.pathname + '?project=' + encodeURIComponent(projectFolder);
                window.history.replaceState({ path: projectFolder }, '', newUrl);
                console.log('[DEBUG] URL 已更新（上传方式），包含项目路径参数');
            }
        } else {
            console.log('[DEBUG] parseProjectXml 返回 null');
        }
    })
    .catch(function (err) {
        console.error('[DEBUG] 请求失败:', err);
        app.setStatus('打开项目失败: ' + err.message);
    })
    .finally(function () {
        app.hideLoading();
    });
};

app.saveProject = function () {
    if (!app.state.project) {
        console.log('[SAVE] 取消: 没有打开的项目');
        app.setStatus('没有打开的项目');
        return;
    }
    console.log('[SAVE] 开始保存流程, project title =', app.state.project.metadata?.title);
    app.showLoading('正在保存项目...');
    try {
        var xmlData = app.serializeProjectToXml();
        // 调试：输出即将保存的 XML 中 BGM 相关内容
        var bgmMatch = xmlData.match(/bgmFile>[^<]+/);
        console.log('[SAVE] 即将保存项目, bgmFile in XML =', bgmMatch ? bgmMatch[0] : '(not found)');
        console.log('[SAVE] XML 长度 =', xmlData.length, ', 前200字符 =', xmlData.substring(0, 200));
        fetch('/api/project/save', {
            method: 'POST',
            headers: { 'Content-Type': 'application/xml' },
            body: xmlData
        })
        .then(function (response) {
            console.log('[SAVE] 后端响应状态: status=', response.status, ' ok=', response.ok);
            if (!response.ok) {
                response.text().then(function (body) {
                    console.error('[SAVE] 后端错误响应 body:', body);
                });
                throw new Error('保存失败 (HTTP ' + response.status + ')');
            }
            app.state.hasUnsavedChanges = false;
            app.setSaveStatus('已保存');
            app.setStatus('项目已保存');
        })
        .catch(function (err) {
            console.error('[SAVE] 保存流程异常:', err.message, err.stack);
            app.setStatus('保存失败: ' + err.message);
        })
        .finally(function () {
            app.hideLoading();
        });
    } catch (e) {
        console.error('[SAVE] serializeProjectToXml 异常:', e.message, e.stack);
        app.setStatus('序列化失败: ' + e.message);
        app.hideLoading();
    }
};

app.updateUI = function () {
    var nameEl = document.getElementById('projectName');
    if (nameEl && app.state.project && app.state.project.metadata) {
        nameEl.textContent = app.state.project.metadata.title || '未命名项目';
        // 根据是否有未保存修改，动态切换星号标记样式
        if (app.state.hasUnsavedChanges) {
            nameEl.classList.add('toolbar-project-name-unsaved');
        } else {
            nameEl.classList.remove('toolbar-project-name-unsaved');
        }
    }
    if (typeof app.updateSceneTabs === 'function') {
        app.updateSceneTabs();
    }
    if (typeof app.updateDialogueList === 'function') {
        app.updateDialogueList();
    }
    if (typeof app.updatePropertyPanel === 'function') {
        app.updatePropertyPanel();
    }
    if (typeof app.updateResourceList === 'function') {
        app.updateResourceList();
    }
    if (typeof app.updateUndoRedoState === 'function') {
        app.updateUndoRedoState();
    }
};

app.undo = function () {
    app.fetchJson('/api/editor/undo', { method: 'POST' })
    .then(function (data) {
        app.updateProjectFromResponse(data);
        app.state.hasUnsavedChanges = true;
        app.setSaveStatus('未保存');
        app.setStatus('已撤销');
        app.updateUI();
    })
    .catch(function (err) {
        app.setStatus('撤销失败: ' + err.message);
    });
};

app.redo = function () {
    app.fetchJson('/api/editor/redo', { method: 'POST' })
    .then(function (data) {
        app.updateProjectFromResponse(data);
        app.state.hasUnsavedChanges = true;
        app.setSaveStatus('未保存');
        app.setStatus('已重做');
        app.updateUI();
    })
    .catch(function (err) {
        app.setStatus('重做失败: ' + err.message);
    });
};

app.addScene = function () {
    if (!app.state.project) {
        app.setStatus('请先新建或打开项目');
        return;
    }
    var sceneCount = app.state.project.scenes ? app.state.project.scenes.length : 0;
    var sceneId = '场景' + (sceneCount + 1);
    app.fetchJson('/api/editor/add-scene', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ sceneId: sceneId })
    })
    .then(function (data) {
        app.updateProjectFromResponse(data);
        app.state.activeSceneIndex = app.state.project.scenes.length - 1;
        app.state.selectedDialogueIndex = -1;
        app.state.hasUnsavedChanges = true;
        app.setSaveStatus('未保存');
        app.setStatus('已添加场景: ' + sceneId);
        app.updateUI();
    })
    .catch(function (err) {
        app.setStatus('添加场景失败: ' + err.message);
    });
};

app.deleteScene = function (sceneIndex) {
    if (!app.state.project) return;
    if (app.state.project.scenes.length <= 1) {
        app.setStatus('至少保留一个场景');
        return;
    }
    app.fetchJson('/api/editor/remove-scene', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ sceneIndex: sceneIndex })
    })
    .then(function (data) {
        app.updateProjectFromResponse(data);
        if (app.state.activeSceneIndex >= app.state.project.scenes.length) {
            app.state.activeSceneIndex = app.state.project.scenes.length - 1;
        }
        app.state.selectedDialogueIndex = -1;
        app.state.hasUnsavedChanges = true;
        app.setSaveStatus('未保存');
        app.setStatus('已删除场景');
        app.updateUI();
    })
    .catch(function (err) {
        app.setStatus('删除场景失败: ' + err.message);
    });
};

app.renameScene = function (sceneIndex) {
    app.state.renamingSceneIndex = sceneIndex;
    var scene = app.state.project.scenes[sceneIndex];
    var input = document.getElementById('renameSceneInput');
    if (input) {
        input.value = scene.id || '';
    }
    var modal = document.getElementById('renameSceneModal');
    if (modal) {
        modal.classList.add('visible');
    }
    if (input) {
        input.focus();
        input.select();
    }
};

app.closeRenameSceneModal = function () {
    var modal = document.getElementById('renameSceneModal');
    if (modal) {
        modal.classList.remove('visible');
    }
    app.state.renamingSceneIndex = -1;
};

app.confirmRenameScene = function () {
    var input = document.getElementById('renameSceneInput');
    var newName = input ? input.value.trim() : '';
    if (!newName) {
        app.setStatus('场景名称不能为空');
        return;
    }
    app.fetchJson('/api/editor/rename-scene', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ sceneIndex: app.state.renamingSceneIndex, newName: newName })
    })
    .then(function (data) {
        app.updateProjectFromResponse(data);
        app.state.hasUnsavedChanges = true;
        app.setSaveStatus('未保存');
        app.setStatus('场景已重命名');
        app.closeRenameSceneModal();
        app.updateUI();
    })
    .catch(function (err) {
        app.setStatus('重命名失败: ' + err.message);
    });
};

app.addDialogue = function () {
    if (!app.state.project) {
        app.setStatus('请先新建或打开项目');
        return;
    }
    app.fetchJson('/api/editor/add-dialogue', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            sceneIndex: app.state.activeSceneIndex,
            type: 'Dialogue',
            insertAfterIndex: app.state.selectedDialogueIndex
        })
    })
    .then(function (data) {
        app.updateProjectFromResponse(data);
        app.state.selectedDialogueIndex = app.state.selectedDialogueIndex + 1;
        if (app.state.selectedDialogueIndex < 0) {
            app.state.selectedDialogueIndex = 0;
        }
        app.state.hasUnsavedChanges = true;
        app.setSaveStatus('未保存');
        app.setStatus('已添加对话');
        app.updateUI();
    })
    .catch(function (err) {
        app.setStatus('添加对话失败: ' + err.message);
    });
};

app.addBranch = function () {
    if (!app.state.project) {
        app.setStatus('请先新建或打开项目');
        return;
    }
    app.fetchJson('/api/editor/add-dialogue', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            sceneIndex: app.state.activeSceneIndex,
            type: 'Branch',
            insertAfterIndex: app.state.selectedDialogueIndex
        })
    })
    .then(function (data) {
        app.updateProjectFromResponse(data);
        app.state.selectedDialogueIndex = app.state.selectedDialogueIndex + 1;
        if (app.state.selectedDialogueIndex < 0) {
            app.state.selectedDialogueIndex = 0;
        }
        app.state.hasUnsavedChanges = true;
        app.setSaveStatus('未保存');
        app.setStatus('已添加分支');
        app.updateUI();
    })
    .catch(function (err) {
        app.setStatus('添加分支失败: ' + err.message);
    });
};

app.addEvent = function () {
    if (!app.state.project) {
        app.setStatus('请先新建或打开项目');
        return;
    }
    app.fetchJson('/api/editor/add-dialogue', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            sceneIndex: app.state.activeSceneIndex,
            type: 'Event',
            insertAfterIndex: app.state.selectedDialogueIndex
        })
    })
    .then(function (data) {
        app.updateProjectFromResponse(data);
        app.state.selectedDialogueIndex = app.state.selectedDialogueIndex + 1;
        if (app.state.selectedDialogueIndex < 0) {
            app.state.selectedDialogueIndex = 0;
        }
        app.state.hasUnsavedChanges = true;
        app.setSaveStatus('未保存');
        app.setStatus('已添加事件');
        app.updateUI();
    })
    .catch(function (err) {
        app.setStatus('添加事件失败: ' + err.message);
    });
};

app.copyDialogue = function () {
    if (!app.state.project || app.state.selectedDialogueIndex < 0) {
        app.setStatus('请先选择要复制的对话');
        return;
    }
    app.fetchJson('/api/editor/copy-dialogue', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            sceneIndex: app.state.activeSceneIndex,
            dialogueIndex: app.state.selectedDialogueIndex
        })
    })
    .then(function (data) {
        app.updateProjectFromResponse(data);
        app.state.selectedDialogueIndex = app.state.selectedDialogueIndex + 1;
        app.state.hasUnsavedChanges = true;
        app.setSaveStatus('未保存');
        app.setStatus('已复制对话');
        app.updateUI();
    })
    .catch(function (err) {
        app.setStatus('复制对话失败: ' + err.message);
    });
};

app.deleteDialogue = function () {
    if (!app.state.project || app.state.selectedDialogueIndex < 0) {
        app.setStatus('请先选择要删除的对话');
        return;
    }
    app.fetchJson('/api/editor/remove-dialogue', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            sceneIndex: app.state.activeSceneIndex,
            dialogueIndex: app.state.selectedDialogueIndex
        })
    })
    .then(function (data) {
        app.updateProjectFromResponse(data);
        app.state.selectedDialogueIndex = -1;
        app.state.hasUnsavedChanges = true;
        app.setSaveStatus('未保存');
        app.setStatus('已删除对话');
        app.updateUI();
    })
    .catch(function (err) {
        app.setStatus('删除对话失败: ' + err.message);
    });
};

app.moveUp = function () {
    if (!app.state.project || app.state.selectedDialogueIndex < 0) {
        app.setStatus('请先选择要移动的对话');
        return;
    }
    app.fetchJson('/api/editor/move-dialogue', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            sceneIndex: app.state.activeSceneIndex,
            dialogueIndex: app.state.selectedDialogueIndex,
            direction: 'up'
        })
    })
    .then(function (data) {
        app.updateProjectFromResponse(data);
        if (app.state.selectedDialogueIndex > 0) {
            app.state.selectedDialogueIndex--;
        }
        app.state.hasUnsavedChanges = true;
        app.setSaveStatus('未保存');
        app.setStatus('已上移');
        app.updateUI();
    })
    .catch(function (err) {
        app.setStatus('上移失败: ' + err.message);
    });
};

app.moveDown = function () {
    if (!app.state.project || app.state.selectedDialogueIndex < 0) {
        app.setStatus('请先选择要移动的对话');
        return;
    }
    app.fetchJson('/api/editor/move-dialogue', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            sceneIndex: app.state.activeSceneIndex,
            dialogueIndex: app.state.selectedDialogueIndex,
            direction: 'down'
        })
    })
    .then(function (data) {
        app.updateProjectFromResponse(data);
        var scene = app.state.project.scenes[app.state.activeSceneIndex];
        if (scene && app.state.selectedDialogueIndex < scene.dialogues.length - 1) {
            app.state.selectedDialogueIndex++;
        }
        app.state.hasUnsavedChanges = true;
        app.setSaveStatus('未保存');
        app.setStatus('已下移');
        app.updateUI();
    })
    .catch(function (err) {
        app.setStatus('下移失败: ' + err.message);
    });
};

app.selectScene = function (index) {
    app.state.activeSceneIndex = index;
    app.state.selectedDialogueIndex = -1;
    if (typeof app.updateSceneTabs === 'function') {
        app.updateSceneTabs();
    }
    if (typeof app.updateDialogueList === 'function') {
        app.updateDialogueList();
    }
    if (typeof app.updatePropertyPanel === 'function') {
        app.updatePropertyPanel();
    }
};

app.selectDialogue = function (index) {
    app.state.selectedDialogueIndex = index;
    if (typeof app.updateDialogueList === 'function') {
        app.updateDialogueList();
    }
    if (typeof app.updatePropertyPanel === 'function') {
        app.updatePropertyPanel();
    }
};

app.runPreview = function () {
    if (!app.state.project) {
        app.setStatus('请先新建或打开项目');
        return;
    }
    // 直接使用前端 state.project 数据构建预览，而非请求后端 API
    // 这确保预览反映的是用户在界面上看到的最新编辑状态（包括未保存的 BGM 修改）
    var previewData = {
        scenes: app.state.project.scenes,
        projectPath: app.state.projectPath || '',
        startSceneIndex: 0,
        startDialogueIndex: 0
    };
    if (typeof app.startPreview === 'function') {
        app.startPreview(previewData);
    }
};

app.previewFromSelected = function () {
    if (!app.state.project) {
        app.setStatus('请先新建或打开项目');
        return;
    }
    if (app.state.selectedDialogueIndex < 0) {
        app.setStatus('请先选择一个对话');
        return;
    }
    // 直接使用前端 state.project 数据，确保预览包含最新的参数修改（如 BGM 选择）
    var previewData = {
        scenes: app.state.project.scenes,
        projectPath: app.state.projectPath || '',
        startSceneIndex: app.state.activeSceneIndex,
        startDialogueIndex: app.state.selectedDialogueIndex
    };
    if (typeof app.startPreview === 'function') {
        app.startPreview(previewData);
    }
};

app.closePreview = function () {
    // 立即停止 BGM 播放（在隐藏 overlay 之前）
    var bgmAudio = document.getElementById('previewBgm');
    if (bgmAudio) {
        bgmAudio.pause();
        bgmAudio.src = '';
    }

    var overlay = document.getElementById('previewOverlay');
    if (overlay) {
        overlay.classList.remove('visible');
    }
    if (typeof app.resetPreviewState === 'function') {
        app.resetPreviewState();
    }
};

app.previewNext = function () {
    if (typeof app.advancePreview === 'function') {
        app.advancePreview();
    }
};

app.previewPrev = function () {
    if (typeof app.goBackPreview === 'function') {
        app.goBackPreview();
    }
};

app.openResourceModal = function () {
    app.state.selectedResourceItem = null;
    app.fetchJson('/api/resources', { method: 'GET' })
    .then(function (data) {
        app.state.cachedResources = data;
        if (typeof app.renderModalResources === 'function') {
            app.renderModalResources(data);
        }
        var modal = document.getElementById('resourceModal');
        if (modal) {
            modal.classList.add('visible');
        }
    })
    .catch(function (err) {
        app.setStatus('加载资源失败: ' + err.message);
    });
};

app.closeResourceModal = function () {
    var modal = document.getElementById('resourceModal');
    if (modal) {
        modal.classList.remove('visible');
    }
    app.state.selectedResourceItem = null;
};

app.switchResourceTab = function (type) {
    app.state.currentResourceTab = type;
    var tabs = document.querySelectorAll('.resource-tab');
    for (var i = 0; i < tabs.length; i++) {
        tabs[i].classList.remove('active');
        if (tabs[i].getAttribute('data-type') === type) {
            tabs[i].classList.add('active');
        }
    }
    if (app.state.cachedResources && typeof app.renderModalResources === 'function') {
        app.renderModalResources(app.state.cachedResources);
    }
};

app.confirmResourceSelection = function () {
    if (app.state.selectedResourceItem && app.state.resourceTargetField) {
        var field = document.getElementById(app.state.resourceTargetField);
        if (field) {
            field.value = app.state.selectedResourceItem;
            field.dispatchEvent(new Event('change'));
        }
    }
    app.closeResourceModal();
};

document.addEventListener('keydown', function (e) {
    if (e.ctrlKey || e.metaKey) {
        switch (e.key.toLowerCase()) {
            case 'z':
                e.preventDefault();
                app.undo();
                break;
            case 'y':
                e.preventDefault();
                app.redo();
                break;
            case 's':
                e.preventDefault();
                app.saveProject();
                break;
            case 'n':
                e.preventDefault();
                app.newProject();
                break;
            case 'o':
                e.preventDefault();
                app.openProject();
                break;
        }
    }

    if (e.key === 'Delete' && !e.ctrlKey && !e.metaKey) {
        var activeEl = document.activeElement;
        var tag = activeEl ? activeEl.tagName.toLowerCase() : '';
        if (tag !== 'input' && tag !== 'textarea' && tag !== 'select') {
            app.deleteDialogue();
        }
    }
});

// ==================== 应用设置（右键菜单）====================

app.showAppSettings = function (e) {
    if (e) e.preventDefault();
    app.showModal('appSettingsModal');
    app.loadAppSettings();
};

app.openAppSettings = function () {
    app.showModal('appSettingsModal');
    app.loadAppSettings();
};

app.loadAppSettings = function () {
    fetch('/api/settings')
        .then(function (response) { return response.json(); })
        .then(function (resp) {
            var data = (resp && resp.data && resp.data.settings) || {};
            if (data.language) {
                var langSelect = document.getElementById('settingsLanguage');
                if (langSelect) langSelect.value = data.language;
            }
            if (data.theme) {
                var themeSelect = document.getElementById('settingsTheme');
                if (themeSelect) themeSelect.value = data.theme;
            }
            if (data.editorFontSize) {
                var fontSizeSelect = document.getElementById('settingsEditorFontSize');
                if (fontSizeSelect) fontSizeSelect.value = String(data.editorFontSize);
            }
            var remoteToggle = document.getElementById('settingsAllowRemoteSession');
            if (remoteToggle) {
                remoteToggle.checked = data.allowRemoteSession === true;
                app.updateRemoteSessionLabel(data.allowRemoteSession === true);
            }
            var isRemote = resp.data && resp.data.isRemoteSession === true;
            app.applyRemoteSessionLock(isRemote);
        })
        .catch(function (err) {
            console.error('Failed to load settings:', err);
        });

    fetch('/api/localization/languages')
        .then(function (response) { return response.json(); })
        .then(function (resp) {
            var langs = (resp && resp.data && resp.data.availableLanguages) || [];
            var langSelect = document.getElementById('settingsLanguage');
            if (!langSelect) return;
            var currentVal = langSelect.value;
            langSelect.innerHTML = '';
            for (var i = 0; i < langs.length; i++) {
                var opt = document.createElement('option');
                opt.value = langs[i].code;
                opt.textContent = langs[i].displayName;
                langSelect.appendChild(opt);
            }
            if (currentVal) langSelect.value = currentVal;
        })
        .catch(function (err) {
            console.error('Failed to load language list:', err);
        });
};

app.saveAppSettings = function () {
    var langSelect = document.getElementById('settingsLanguage');
    var themeSelect = document.getElementById('settingsTheme');
    var fontSizeSelect = document.getElementById('settingsEditorFontSize');
    var remoteToggle = document.getElementById('settingsAllowRemoteSession');

    var settings = {};
    if (langSelect) settings.language = langSelect.value;
    if (themeSelect) settings.theme = themeSelect.value;
    if (fontSizeSelect) settings.editorFontSize = parseInt(fontSizeSelect.value, 10);
    if (remoteToggle) settings.allowRemoteSession = remoteToggle.checked;

    fetch('/api/settings', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ settings: settings })
    })
    .then(function (response) {
        if (!response.ok) throw new Error('HTTP ' + response.status);
        return response.json();
    })
    .then(function (resp) {
        app.closeAppSettings();

        var result = (resp && resp.data) || {};

        if (result.themeChanged) {
            app.applyTheme(settings.theme);
        }

        if (settings.editorFontSize) {
            app.applyFontSize(settings.editorFontSize);
        }

        if (result.languageChanged) {
            app.refreshPageContent();
        }
    })
    .catch(function (err) {
        console.error('Failed to save settings:', err);
        app.setStatus('Save settings failed: ' + err.message);
    });
};

app.applyTheme = function (theme) {
    var root = document.documentElement;
    root.classList.remove('theme-light', 'theme-dark');
    root.classList.add('theme-' + (theme || 'light'));
};

app.applyFontSize = function (fontSize) {
    document.documentElement.style.setProperty('--editor-font-size', fontSize + 'px');
    var editor = document.getElementById('dialogueList');
    if (editor) editor.style.fontSize = fontSize + 'px';
};

app.refreshPageContent = function () {
    window.location.reload();
};

app.closeAppSettings = function () {
    app.hideModal('appSettingsModal');
};

// ==================== 模态框动画工具 ====================

app.showModal = function (id) {
    var modal = document.getElementById(id);
    if (!modal) return;
    modal.style.display = 'flex';
    modal.offsetHeight;
    modal.classList.add('visible');
    modal.classList.remove('fade-out');
};

app.hideModal = function (id) {
    var modal = document.getElementById(id);
    if (!modal) return;
    modal.classList.add('fade-out');
    modal.classList.remove('visible');
    setTimeout(function () {
        modal.style.display = 'none';
        modal.classList.remove('fade-out');
    }, 200);
};

// ==================== 远程访问开关逻辑 ====================

app.updateRemoteSessionLabel = function (enabled) {
    var label = document.getElementById('settingsAllowRemoteSessionLabel');
    if (label) label.textContent = enabled ? 'ON' : 'OFF';
};

app.confirmRemoteAccess = function () {
    app.clearRemoteAccessCountdown();
    var toggle = document.getElementById('settingsAllowRemoteSession');
    if (toggle) {
        toggle.checked = true;
        app.updateRemoteSessionLabel(true);
    }
    app.hideModal('remoteAccessConfirmModal');
};

app.cancelRemoteAccess = function () {
    app.clearRemoteAccessCountdown();
    var toggle = document.getElementById('settingsAllowRemoteSession');
    if (toggle) {
        toggle.checked = false;
        app.updateRemoteSessionLabel(false);
    }
    app.hideModal('remoteAccessConfirmModal');
};

app._remoteAccessCountdownTimer = null;

app.startRemoteAccessCountdown = function () {
    app.clearRemoteAccessCountdown();
    var btn = document.getElementById('remoteAccessConfirmBtn');
    if (!btn) return;
    btn.disabled = true;
    var originalText = btn.getAttribute('data-original-text') || btn.textContent;
    btn.setAttribute('data-original-text', originalText);
    var seconds = 5;
    btn.textContent = originalText + ' (' + seconds + 's)';
    app._remoteAccessCountdownTimer = setInterval(function () {
        seconds--;
        if (seconds <= 0) {
            app.clearRemoteAccessCountdown();
            btn.disabled = false;
            btn.textContent = originalText;
        } else {
            btn.textContent = originalText + ' (' + seconds + 's)';
        }
    }, 1000);
};

app.clearRemoteAccessCountdown = function () {
    if (app._remoteAccessCountdownTimer) {
        clearInterval(app._remoteAccessCountdownTimer);
        app._remoteAccessCountdownTimer = null;
    }
};

app.applyRemoteSessionLock = function (isRemote) {
    var toggle = document.getElementById('settingsAllowRemoteSession');
    var hint = document.getElementById('settingsRemoteSessionLockedHint');
    if (isRemote) {
        if (toggle) {
            toggle.checked = true;
            toggle.disabled = true;
            app.updateRemoteSessionLabel(true);
        }
        if (hint) hint.style.display = 'block';
    } else {
        if (toggle) toggle.disabled = false;
        if (hint) hint.style.display = 'none';
    }
};

// ==================== 退出应用功能 ====================

app.exitApplication = function () {
    var textEl = document.getElementById('exitConfirmText');
    if (textEl) {
        if (app.state.hasUnsavedChanges) {
            textEl.innerHTML = '确定要退出 Visunovia 吗？<br/><br/>⚠ <strong style="color:#F59E0B;">当前项目有未保存的修改！</strong><br/><br/>此操作将关闭当前标签页并停止后端服务。';
        } else {
            textEl.innerHTML = '确定要退出 Visunovia 吗？<br/><br/>此操作将关闭当前标签页并停止后端服务。';
        }
    }
    app.showModal('exitConfirmModal');
};

app.closeExitConfirm = function () {
    app.hideModal('exitConfirmModal');
};

app.confirmExit = function () {
    // 先请求后端关闭服务
    fetch('/api/system/shutdown', { method: 'POST' })
        .then(function () {
            // 后端已响应，关闭标签页
            window.close();
            // 如果 window.close() 被浏览器阻止（非脚本打开的页面），给出提示
            setTimeout(function () {
                alert('后端服务已停止。请手动关闭此标签页。');
            }, 500);
        })
        .catch(function () {
            // 即使 shutdown 请求失败也尝试关闭
            window.close();
            setTimeout(function () {
                alert('无法连接到后端服务。请手动关闭此标签页。');
            }, 500);
        });
};

// ==================== 初始化事件绑定 ====================

document.addEventListener('DOMContentLoaded', function () {
    // 应用标题右键 → 打开设置
    var appTitle = document.getElementById('appTitle');
    if (appTitle) {
        appTitle.addEventListener('contextmenu', function (e) {
            e.preventDefault();
            app.showAppSettings();
        });
    }

    // 点击模态框外部区域关闭
    [ 'appSettingsModal', 'exitConfirmModal', 'remoteAccessConfirmModal' ].forEach(function (id) {
        var el = document.getElementById(id);
        if (el) {
            el.addEventListener('click', function (e) {
                if (e.target === this) {
                    app.hideModal(id);
                }
            });
        }
    });

    // 远程访问开关点击事件
    var remoteToggle = document.getElementById('settingsAllowRemoteSession');
    if (remoteToggle) {
        remoteToggle.addEventListener('click', function (e) {
            if (this.checked) {
                e.preventDefault();
                app.showModal('remoteAccessConfirmModal');
                app.startRemoteAccessCountdown();
            } else {
                app.updateRemoteSessionLabel(false);
            }
        });
    }

    // 自动恢复项目：如果 URL 包含 project 参数，页面刷新后自动重新加载
    (function () {
        var urlParams = new URLSearchParams(window.location.search);
        var projectParam = urlParams.get('project');
        if (projectParam) {
            console.log('[INIT] 检测到 URL 项目参数，自动恢复项目:', projectParam);
            setTimeout(function () {
                if (!app.state.project) {
                    console.log('[INIT] 开始自动加载项目...');
                    app.showLoading('正在恢复项目...');
                    fetch('/api/files/openProject', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ path: decodeURIComponent(projectParam) })
                    })
                    .then(function (response) {
                        if (!response.ok) throw new Error('HTTP ' + response.status);
                        return response.text();
                    })
                    .then(function (xmlString) {
                        var data = app.parseProjectXml(xmlString);
                        if (data) {
                            app.state.project = data;
                            app.state.activeSceneIndex = 0;
                            app.state.selectedDialogueIndex = -1;
                            app.state.hasUnsavedChanges = false;
                            app.state.projectPath = decodeURIComponent(projectParam);
                            app.setStatus('项目已恢复: ' + (data.metadata ? data.metadata.title : '项目'));
                            app.setSaveStatus('已保存');
                            app.updateUI();
                            console.log('[INIT] 项目自动恢复成功');
                        }
                    })
                    .catch(function (err) {
                        console.error('[INIT] 自动恢复项目失败:', err.message);
                        app.setStatus('项目恢复失败，请手动打开: ' + err.message);
                    })
                    .finally(function () {
                        app.hideLoading();
                    });
                }
            }, 300);
        }
    })();

    // 防止意外刷新导致未保存的项目数据丢失
    window.addEventListener('beforeunload', function (e) {
        if (app.state.project && app.state.hasUnsavedChanges) {
            e.preventDefault();
            e.returnValue = '您有未保存的更改，确定要离开吗？';
            return e.returnValue;
        }
    });
});
