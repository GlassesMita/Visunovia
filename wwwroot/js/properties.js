var app = app || {};

app.updatePropertyPanel = function () {
    var container = document.getElementById('propertyPanel');
    if (!container) return;

    container.innerHTML = '';

    if (!app.state.project || app.state.selectedDialogueIndex < 0) {
        container.innerHTML = '<p style="color: #666; font-size: 12px;">选择对话以编辑属性</p>';
        return;
    }

    var sceneIndex = app.state.activeSceneIndex;
    if (sceneIndex < 0 || sceneIndex >= app.state.project.scenes.length) {
        container.innerHTML = '<p style="color: #666; font-size: 12px;">选择对话以编辑属性</p>';
        return;
    }

    var scene = app.state.project.scenes[sceneIndex];
    if (!scene || !scene.dialogues || app.state.selectedDialogueIndex >= scene.dialogues.length) {
        container.innerHTML = '<p style="color: #666; font-size: 12px;">选择对话以编辑属性</p>';
        return;
    }

    var dialogue = scene.dialogues[app.state.selectedDialogueIndex];
    var dialogueType = dialogue.type;
    if (typeof dialogueType === 'number') {
        dialogueType = ['Dialogue', 'Branch', 'Event'][dialogueType] || 'Dialogue';
    }

    app.fetchResourcesForProperties(function (resources) {
        if (dialogueType === 'Dialogue') {
            app.renderDialogueProperties(container, dialogue, resources);
        } else if (dialogueType === 'Branch') {
            app.renderBranchProperties(container, dialogue, resources);
        } else if (dialogueType === 'Event') {
            app.renderEventProperties(container, dialogue, resources);
        }
    });
};

app.fetchResourcesForProperties = function (callback) {
    if (app.state.cachedResources) {
        callback(app.state.cachedResources);
        return;
    }
    fetch('/api/resources', { method: 'GET' })
        .then(function (res) { return res.json(); })
        .then(function (data) {
            app.state.cachedResources = data;
            callback(data);
        })
        .catch(function () {
            callback({ sprites: [], backgrounds: [], bgm: [], voice: [], sfx: [] });
        });
};

app.renderDialogueProperties = function (container, dialogue, resources) {
    var html = '';

    html += '<div class="property-section">';
    html += '<div class="property-label">说话人</div>';
    html += '<input type="text" id="propSpeaker" value="' + app.escapeAttr(dialogue.speaker || '') + '" />';
    html += '</div>';

    html += '<div class="property-section">';
    html += '<div class="property-label">对话文本</div>';
    html += '<textarea id="propText">' + app.escapeHtml(dialogue.text || '') + '</textarea>';
    html += '</div>';

    html += '<div class="property-section">';
    html += '<div class="property-label">背景</div>';
    html += '<div style="display:flex;gap:4px;">';
    html += '<select id="propBackground" style="flex:1;">';
    html += '<option value="">(无)</option>';
    if (resources.backgrounds) {
        for (var i = 0; i < resources.backgrounds.length; i++) {
            var sel = dialogue.background === resources.backgrounds[i] ? ' selected' : '';
            html += '<option value="' + app.escapeAttr(resources.backgrounds[i]) + '"' + sel + '>' + app.escapeHtml(resources.backgrounds[i]) + '</option>';
        }
    }
    html += '</select>';
    html += '<button class="toolbar-btn" style="flex-shrink:0;padding:4px 8px;font-size:11px;" onclick="app.state.resourceTargetField=\'propBackground\';app.openResourceModal();">...</button>';
    html += '</div>';
    html += '</div>';

    var spritePath = '';
    if (dialogue.sprites && dialogue.sprites.length > 0) {
        spritePath = dialogue.sprites[0].path || '';
    }
    html += '<div class="property-section">';
    html += '<div class="property-label">立绘</div>';
    html += '<div style="display:flex;gap:4px;">';
    html += '<select id="propSprite" style="flex:1;">';
    html += '<option value="">(无)</option>';
    if (resources.sprites) {
        for (var i = 0; i < resources.sprites.length; i++) {
            var sel = spritePath === resources.sprites[i] ? ' selected' : '';
            html += '<option value="' + app.escapeAttr(resources.sprites[i]) + '"' + sel + '>' + app.escapeHtml(resources.sprites[i]) + '</option>';
        }
    }
    html += '</select>';
    html += '<button class="toolbar-btn" style="flex-shrink:0;padding:4px 8px;font-size:11px;" onclick="app.state.resourceTargetField=\'propSprite\';app.openResourceModal();">...</button>';
    html += '</div>';
    html += '</div>';

    html += '<div class="property-section">';
    html += '<div class="property-label">语音</div>';
    html += '<div style="display:flex;gap:4px;">';
    html += '<select id="propVoice" style="flex:1;">';
    html += '<option value="">(无)</option>';
    if (resources.voice) {
        for (var i = 0; i < resources.voice.length; i++) {
            var sel = dialogue.voice === resources.voice[i] ? ' selected' : '';
            html += '<option value="' + app.escapeAttr(resources.voice[i]) + '"' + sel + '>' + app.escapeHtml(resources.voice[i]) + '</option>';
        }
    }
    html += '</select>';
    html += '<button class="toolbar-btn" style="flex-shrink:0;padding:4px 8px;font-size:11px;" onclick="app.state.resourceTargetField=\'propVoice\';app.openResourceModal();">...</button>';
    html += '</div>';
    html += '</div>';

    container.innerHTML = html;

    document.getElementById('propSpeaker').addEventListener('change', function () {
        app.onDialoguePropertyChange('speaker', this.value);
    });
    document.getElementById('propText').addEventListener('change', function () {
        app.onDialoguePropertyChange('text', this.value);
    });
    document.getElementById('propBackground').addEventListener('change', function () {
        app.onDialoguePropertyChange('background', this.value);
    });
    document.getElementById('propSprite').addEventListener('change', function () {
        app.onDialoguePropertyChange('sprite', this.value);
    });
    document.getElementById('propVoice').addEventListener('change', function () {
        app.onDialoguePropertyChange('voice', this.value);
    });
};

app.renderBranchProperties = function (container, dialogue, resources) {
    var html = '';
    var choices = (dialogue.branch && dialogue.branch.choices) || [];

    html += '<div class="property-section">';
    html += '<div class="property-label">分支选项</div>';
    html += '</div>';

    for (var i = 0; i < choices.length; i++) {
        html += '<div class="property-section" style="border:1px solid #333;padding:8px;border-radius:4px;margin-bottom:8px;">';
        html += '<div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:6px;">';
        html += '<span style="color:#10B981;font-size:12px;">选项 ' + (i + 1) + '</span>';
        html += '<button class="red-btn" style="font-size:10px;padding:2px 6px;" onclick="app.removeBranchChoice(' + i + ')">删除</button>';
        html += '</div>';
        html += '<div class="property-label">选项文本</div>';
        html += '<input type="text" class="choice-text" data-index="' + i + '" value="' + app.escapeAttr(choices[i].text || '') + '" />';
        html += '<div class="property-label" style="margin-top:6px;">目标场景</div>';
        html += '<select class="choice-target" data-index="' + i + '">';
        html += '<option value="">(当前场景)</option>';
        if (app.state.project && app.state.project.scenes) {
            for (var s = 0; s < app.state.project.scenes.length; s++) {
                var sceneId = app.state.project.scenes[s].id || '';
                var sel = choices[i].targetScene === sceneId ? ' selected' : '';
                html += '<option value="' + app.escapeAttr(sceneId) + '"' + sel + '>' + app.escapeHtml(sceneId) + '</option>';
            }
        }
        html += '</select>';
        html += '</div>';
    }

    html += '<button class="green-btn" style="width:100%;padding:6px;font-size:12px;margin-top:4px;" onclick="app.addBranchChoice()">+ 添加选项</button>';

    container.innerHTML = html;

    var textInputs = container.querySelectorAll('.choice-text');
    for (var i = 0; i < textInputs.length; i++) {
        textInputs[i].addEventListener('change', function () {
            var idx = parseInt(this.getAttribute('data-index'), 10);
            app.onBranchChoiceChange(idx, 'text', this.value);
        });
    }

    var targetSelects = container.querySelectorAll('.choice-target');
    for (var i = 0; i < targetSelects.length; i++) {
        targetSelects[i].addEventListener('change', function () {
            var idx = parseInt(this.getAttribute('data-index'), 10);
            app.onBranchChoiceChange(idx, 'targetScene', this.value);
        });
    }
};

app.renderEventProperties = function (container, dialogue, resources) {
    var evt = dialogue.event || { eventType: 0, parameters: {} };
    var eventType = evt.eventType;
    if (typeof eventType === 'number') {
        eventType = ['JumpScene', 'SetVariable', 'PlaySound', 'ChangeBackground', 'ChangeBgm', 'ShowCharacter', 'HideCharacter', 'Pause', 'WaitSeconds', 'WindowEffect', 'Custom', 'SendSystemNotification'][eventType] || 'Custom';
    }
    var params = evt.parameters || {};

    var html = '';

    html += '<div class="property-section">';
    html += '<div class="property-label">事件类型</div>';
    html += '<select id="propEventType">';
    var eventTypes = ['JumpScene', 'SetVariable', 'PlaySound', 'ChangeBackground', 'ChangeBgm', 'ShowCharacter', 'HideCharacter', 'Pause', 'WaitSeconds', 'WindowEffect', 'Custom', 'SendSystemNotification'];
    for (var i = 0; i < eventTypes.length; i++) {
        var sel = eventType === eventTypes[i] ? ' selected' : '';
        html += '<option value="' + eventTypes[i] + '"' + sel + '>' + eventTypes[i] + '</option>';
    }
    html += '</select>';
    html += '</div>';

    html += '<div id="eventParamsContainer">';
    html += app.renderEventParams(eventType, params, resources);
    html += '</div>';

    container.innerHTML = html;

    // 使用命名回调防止重复绑定（每次 renderEventProperties 重建 DOM 后需重新绑定）
    if (!app._eventTypeChangeHandler) {
        app._eventTypeChangeHandler = function () {
            app.onEventTypeChange(this.value, resources);
        };
    }
    var eventTypeEl = document.getElementById('propEventType');
    if (eventTypeEl) {
        eventTypeEl.removeEventListener('change', app._eventTypeChangeHandler);
        eventTypeEl.addEventListener('change', app._eventTypeChangeHandler);
    }
};

app.renderEventParams = function (eventType, params, resources) {
    var html = '';

    switch (eventType) {
        case 'JumpScene':
            html += '<div class="property-section">';
            html += '<div class="property-label">目标场景</div>';
            html += '<select id="eventParam_targetScene">';
            html += '<option value="">(选择场景)</option>';
            if (app.state.project && app.state.project.scenes) {
                for (var s = 0; s < app.state.project.scenes.length; s++) {
                    var sceneId = app.state.project.scenes[s].id || '';
                    var sel = (params.targetScene || '') === sceneId ? ' selected' : '';
                    html += '<option value="' + app.escapeAttr(sceneId) + '"' + sel + '>' + app.escapeHtml(sceneId) + '</option>';
                }
            }
            html += '</select>';
            html += '</div>';
            break;

        case 'SetVariable':
            html += '<div class="property-section">';
            html += '<div class="property-label">变量名</div>';
            html += '<input type="text" id="eventParam_variableName" value="' + app.escapeAttr(params.variableName || '') + '" />';
            html += '</div>';
            html += '<div class="property-section">';
            html += '<div class="property-label">变量值</div>';
            html += '<input type="text" id="eventParam_variableValue" value="' + app.escapeAttr(params.variableValue != null ? String(params.variableValue) : '') + '" />';
            html += '</div>';
            break;

        case 'PlaySound':
            html += '<div class="property-section">';
            html += '<div class="property-label">音效文件</div>';
            html += '<div style="display:flex;gap:4px;">';
            html += '<select id="eventParam_soundFile" style="flex:1;">';
            html += '<option value="">(无)</option>';
            if (resources.sfx) {
                for (var i = 0; i < resources.sfx.length; i++) {
                    var sel = (params.soundFile || '') === resources.sfx[i] ? ' selected' : '';
                    html += '<option value="' + app.escapeAttr(resources.sfx[i]) + '"' + sel + '>' + app.escapeHtml(resources.sfx[i]) + '</option>';
                }
            }
            html += '</select>';
            html += '<button class="toolbar-btn" style="flex-shrink:0;padding:4px 8px;font-size:11px;" onclick="app.state.resourceTargetField=\'eventParam_soundFile\';app.openResourceModal();">...</button>';
            html += '</div>';
            html += '</div>';
            break;

        case 'ChangeBackground':
            html += '<div class="property-section">';
            html += '<div class="property-label">背景</div>';
            html += '<div style="display:flex;gap:4px;">';
            html += '<select id="eventParam_background" style="flex:1;">';
            html += '<option value="">(无)</option>';
            if (resources.backgrounds) {
                for (var i = 0; i < resources.backgrounds.length; i++) {
                    var sel = (params.background || '') === resources.backgrounds[i] ? ' selected' : '';
                    html += '<option value="' + app.escapeAttr(resources.backgrounds[i]) + '"' + sel + '>' + app.escapeHtml(resources.backgrounds[i]) + '</option>';
                }
            }
            html += '</select>';
            html += '<button class="toolbar-btn" style="flex-shrink:0;padding:4px 8px;font-size:11px;" onclick="app.state.resourceTargetField=\'eventParam_background\';app.openResourceModal();">...</button>';
            html += '</div>';
            html += '</div>';
            break;

        case 'ChangeBgm':
            // 解码可能的多重 XML 实体编码，确保与资源列表中的原始文件名正确匹配
            var rawBgmFile = params.bgmFile || '';
            var decodedBgmFile = rawBgmFile;
            var prevBgm;
            var bgmMaxIter = 10;
            do {
                prevBgm = decodedBgmFile;
                decodedBgmFile = decodedBgmFile
                    .replace(/&apos;/g, "'")
                    .replace(/&quot;/g, '"')
                    .replace(/&amp;/g, '&')
                    .replace(/&lt;/g, '<')
                    .replace(/&gt;/g, '>');
                bgmMaxIter--;
            } while (decodedBgmFile !== prevBgm && bgmMaxIter > 0);
            // 诊断日志：输出渲染时读取到的 bgmFile 参数值，便于追踪数据丢失时机
            console.log('[BGM-DEBUG] renderEventParams: params.bgmFile =',
                params.hasOwnProperty('bgmFile') ? '"' + params.bgmFile + '"' : '(key 不存在)',
                '| decoded =', '"' + decodedBgmFile + '"',
                '| full params =', JSON.stringify(params));
            html += '<div class="property-section">';
            html += '<div class="property-label">背景音乐</div>';
            html += '<div style="display:flex;gap:4px;">';
            html += '<select id="eventParam_bgmFile" style="flex:1;">';
            html += '<option value="">(无)</option>';
            if (resources.bgm) {
                for (var i = 0; i < resources.bgm.length; i++) {
                    // 使用解码后的值进行匹配，兼容历史遗留的多重编码数据
                    var sel = decodedBgmFile === resources.bgm[i] ? ' selected' : '';
                    html += '<option value="' + app.escapeAttr(resources.bgm[i]) + '"' + sel + '>' + app.escapeHtml(resources.bgm[i]) + '</option>';
                }
            }
            html += '</select>';
            html += '<button class="toolbar-btn" style="flex-shrink:0;padding:4px 8px;font-size:11px;" onclick="app.state.resourceTargetField=\'eventParam_bgmFile\';app.openResourceModal();">...</button>';
            html += '</div>';
            html += '</div>';
            break;

        case 'ShowCharacter':
        case 'HideCharacter':
            html += '<div class="property-section">';
            html += '<div class="property-label">角色立绘</div>';
            html += '<div style="display:flex;gap:4px;">';
            html += '<select id="eventParam_character" style="flex:1;">';
            html += '<option value="">(无)</option>';
            if (resources.sprites) {
                for (var i = 0; i < resources.sprites.length; i++) {
                    var sel = (params.character || '') === resources.sprites[i] ? ' selected' : '';
                    html += '<option value="' + app.escapeAttr(resources.sprites[i]) + '"' + sel + '>' + app.escapeHtml(resources.sprites[i]) + '</option>';
                }
            }
            html += '</select>';
            html += '<button class="toolbar-btn" style="flex-shrink:0;padding:4px 8px;font-size:11px;" onclick="app.state.resourceTargetField=\'eventParam_character\';app.openResourceModal();">...</button>';
            html += '</div>';
            html += '</div>';
            break;

        case 'Pause':
        case 'WaitSeconds':
            html += '<div class="property-section">';
            html += '<div class="property-label">持续时间 (毫秒)</div>';
            html += '<input type="number" id="eventParam_duration" value="' + app.escapeAttr(params.duration != null ? String(params.duration) : '1000') + '" min="0" />';
            html += '</div>';
            break;

        case 'SendSystemNotification':
            html += '<div class="property-section">';
            html += '<div class="property-label">标题</div>';
            html += '<input type="text" id="eventParam_title" value="' + app.escapeAttr(params.title || '') + '" />';
            html += '</div>';
            html += '<div class="property-section">';
            html += '<div class="property-label">正文</div>';
            html += '<input type="text" id="eventParam_message" value="' + app.escapeAttr(params.message || '') + '" />';
            html += '</div>';
            break;

        case 'Custom':
            html += '<div class="property-section">';
            html += '<div class="property-label">自定义命令</div>';
            html += '<textarea id="eventParam_command">' + app.escapeHtml(params.command || '') + '</textarea>';
            html += '</div>';
            break;
    }

    return html;
};

app.onDialoguePropertyChange = function (field, value) {
    var scene = app.state.project.scenes[app.state.activeSceneIndex];
    if (!scene) return;
    var dialogue = scene.dialogues[app.state.selectedDialogueIndex];
    if (!dialogue) return;

    switch (field) {
        case 'speaker':
            dialogue.speaker = value;
            break;
        case 'text':
            dialogue.text = value;
            break;
        case 'background':
            dialogue.background = value;
            break;
        case 'sprite':
            if (!dialogue.sprites) dialogue.sprites = [];
            if (dialogue.sprites.length === 0) {
                dialogue.sprites.push({ path: value, position: 'center', layer: 0, animation: { type: 'none', duration: 300 } });
            } else {
                dialogue.sprites[0].path = value;
            }
            break;
        case 'voice':
            dialogue.voice = value;
            break;
    }

    app.sendDialogueUpdate(dialogue);
};

app.onBranchChoiceChange = function (choiceIndex, field, value) {
    var scene = app.state.project.scenes[app.state.activeSceneIndex];
    if (!scene) return;
    var dialogue = scene.dialogues[app.state.selectedDialogueIndex];
    if (!dialogue || !dialogue.branch || !dialogue.branch.choices) return;
    if (choiceIndex < 0 || choiceIndex >= dialogue.branch.choices.length) return;

    dialogue.branch.choices[choiceIndex][field] = value;
    app.sendDialogueUpdate(dialogue);
};

app.addBranchChoice = function () {
    var scene = app.state.project.scenes[app.state.activeSceneIndex];
    if (!scene) return;
    var dialogue = scene.dialogues[app.state.selectedDialogueIndex];
    if (!dialogue) return;
    if (!dialogue.branch) dialogue.branch = { choices: [] };
    if (!dialogue.branch.choices) dialogue.branch.choices = [];

    dialogue.branch.choices.push({ text: '', targetScene: '', condition: '' });
    app.sendDialogueUpdate(dialogue);
};

app.removeBranchChoice = function (choiceIndex) {
    var scene = app.state.project.scenes[app.state.activeSceneIndex];
    if (!scene) return;
    var dialogue = scene.dialogues[app.state.selectedDialogueIndex];
    if (!dialogue || !dialogue.branch || !dialogue.branch.choices) return;

    dialogue.branch.choices.splice(choiceIndex, 1);
    app.sendDialogueUpdate(dialogue);
};

app.onEventTypeChange = function (newEventType, resources) {
    var scene = app.state.project.scenes[app.state.activeSceneIndex];
    if (!scene) return;
    var dialogue = scene.dialogues[app.state.selectedDialogueIndex];
    if (!dialogue) return;
    if (!dialogue.event) dialogue.event = { eventType: 'Custom', parameters: {} };

    // 将旧类型统一为字符串格式以便比较（兼容数字枚举和字符串两种形式）
    var oldEventType = dialogue.event.eventType;
    if (typeof oldEventType === 'number') {
        var typeNames = ['JumpScene', 'SetVariable', 'PlaySound', 'ChangeBackground',
            'ChangeBgm', 'ShowCharacter', 'HideCharacter', 'Pause', 'WaitSeconds', 'WindowEffect', 'Custom', 'SendSystemNotification'];
        oldEventType = typeNames[oldEventType] || 'Custom';
    }

    // ★ 核心修复：仅在事件类型真正改变时才重置参数并同步后端
    // 之前无条件清空参数导致空 {} 被写入后端内存，污染后续所有操作的数据
    if (newEventType !== oldEventType) {
        dialogue.event.eventType = newEventType;
        dialogue.event.parameters = {};

        var paramsContainer = document.getElementById('eventParamsContainer');
        if (paramsContainer) {
            paramsContainer.innerHTML = app.renderEventParams(newEventType, {}, resources);
            app.bindEventParamListeners(resources);
        }
        app.sendDialogueUpdate(dialogue);
    }
    // 类型未变 → 保留现有参数不变，不发送任何请求，避免空参数覆盖有效值
};

app.bindEventParamListeners = function (resources) {
    var paramIds = [
        'eventParam_targetScene', 'eventParam_variableName', 'eventParam_variableValue',
        'eventParam_soundFile', 'eventParam_background', 'eventParam_bgmFile',
        'eventParam_character', 'eventParam_duration', 'eventParam_command',
        'eventParam_title', 'eventParam_message'
    ];

    // 使用命名回调函数，以便后续可以移除旧监听器防止重复绑定
    // 每次 renderEventParams 重建 DOM 后必须重新绑定，但先移除旧的避免累积
    if (!app._paramChangeHandler) {
        app._paramChangeHandler = function () {
            app.collectAndSendEventParams();
        };
    }

    for (var i = 0; i < paramIds.length; i++) {
        var el = document.getElementById(paramIds[i]);
        if (el) {
            // 先移除可能存在的旧监听器（防止 renderEventParams 多次调用时重复绑定）
            el.removeEventListener('change', app._paramChangeHandler);
            el.addEventListener('change', app._paramChangeHandler);
        }
    }
};

app.collectAndSendEventParams = function () {
    var scene = app.state.project.scenes[app.state.activeSceneIndex];
    if (!scene) return;
    var dialogue = scene.dialogues[app.state.selectedDialogueIndex];
    if (!dialogue || !dialogue.event) return;

    var params = {};

    var targetSceneEl = document.getElementById('eventParam_targetScene');
    if (targetSceneEl) params.targetScene = targetSceneEl.value;

    var variableNameEl = document.getElementById('eventParam_variableName');
    if (variableNameEl) params.variableName = variableNameEl.value;

    var variableValueEl = document.getElementById('eventParam_variableValue');
    if (variableValueEl) params.variableValue = variableValueEl.value;

    var soundFileEl = document.getElementById('eventParam_soundFile');
    if (soundFileEl) params.soundFile = soundFileEl.value;

    var backgroundEl = document.getElementById('eventParam_background');
    if (backgroundEl) params.background = backgroundEl.value;

    var bgmFileEl = document.getElementById('eventParam_bgmFile');
    if (bgmFileEl) params.bgmFile = bgmFileEl.value;

    var characterEl = document.getElementById('eventParam_character');
    if (characterEl) params.character = characterEl.value;

    var durationEl = document.getElementById('eventParam_duration');
    if (durationEl) params.duration = parseInt(durationEl.value, 10) || 0;

    var titleEl = document.getElementById('eventParam_title');
    if (titleEl) params.title = titleEl.value;

    var messageEl = document.getElementById('eventParam_message');
    if (messageEl) params.message = messageEl.value;

    var commandEl = document.getElementById('eventParam_command');
    if (commandEl) params.command = commandEl.value;

    console.log('[DEBUG] collectAndSendEventParams - params:', params, '| dialogue.event.parameters before:', JSON.stringify(dialogue.event.parameters));

    var oldParamsJson = JSON.stringify(dialogue.event.parameters || {});
    var newParamsJson = JSON.stringify(params);
    if (oldParamsJson === newParamsJson) {
        console.log('[DEBUG] 参数未变化，跳过发送');
        return;
    }

    dialogue.event.parameters = params;
    console.log('[DEBUG] 发送参数到后端:', params);
    app.sendDialogueUpdate(dialogue);
};

app.sendDialogueUpdate = function (dialogue) {
    var dialogueCopy = JSON.parse(JSON.stringify(dialogue));

    // 请求版本控制：递增版本号，响应到达时检查是否为最新版本
    // 解决竞态条件问题：onEventTypeChange 发送的空参数请求可能延迟到达，
    // 覆盖用户后续 collectAndSendEventParams 发送的有效参数
    app._dialogueUpdateVersion = (app._dialogueUpdateVersion || 0) + 1;
    var currentVersion = app._dialogueUpdateVersion;

    app.fetchJson('/api/editor/update-dialogue', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            sceneIndex: app.state.activeSceneIndex,
            dialogueIndex: app.state.selectedDialogueIndex,
            dialogue: dialogueCopy
        })
    })
    .then(function (data) {
        // 丢弃过期响应：如果当前版本号已大于此响应的版本，说明有更新的请求已发出
        if (currentVersion !== app._dialogueUpdateVersion) {
            console.log('[DEBUG] sendDialogueUpdate: 丢弃过期响应 (v' + currentVersion + ' < v' + app._dialogueUpdateVersion + ')');
            return;
        }
        // 使用 normalizeProjectData 标准化数据格式（枚举数字→字符串、属性名映射等）
        app.state.project = app.normalizeProjectData(data);
        app.state.hasUnsavedChanges = true;
        app.setSaveStatus('未保存');
        // 刷新所有 UI 组件（包括属性面板），确保显示最新数据
        app.updateUI();
    })
    .catch(function (err) {
        // 同样丢弃过期的错误响应
        if (currentVersion !== app._dialogueUpdateVersion) return;
        app.setStatus('更新对话失败: ' + err.message);
    });
};

app.escapeAttr = function (text) {
    if (!text) return '';
    return String(text)
        .replace(/&/g, '&amp;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;');
};
