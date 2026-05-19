var app = app || {};

app.previewState = {
    scenes: null,
    projectPath: '',
    currentSceneIndex: 0,
    currentDialogueIndex: -1,
    history: [],
    currentBg: '',
    currentChar: '',
    currentBgm: ''
};

app.startPreview = function (data) {
    if (!data || !data.scenes || data.scenes.length === 0) {
        app.setStatus('没有可预览的内容');
        return;
    }

    app.previewState.scenes = data.scenes;
    app.previewState.projectPath = data.projectPath || '';
    app.previewState.currentSceneIndex = data.startSceneIndex || 0;
    app.previewState.currentDialogueIndex = -1;
    app.previewState.history = [];
    app.previewState.currentBg = '';
    app.previewState.currentChar = '';
    app.previewState.currentBgm = '';

    var overlay = document.getElementById('previewOverlay');
    if (overlay) {
        overlay.classList.add('visible');
    }

    app.advancePreview();
};

app.advancePreview = function () {
    var state = app.previewState;
    if (!state.scenes || state.scenes.length === 0) return;

    var sceneIndex = state.currentSceneIndex;
    if (sceneIndex < 0 || sceneIndex >= state.scenes.length) {
        app.closePreview();
        return;
    }

    var scene = state.scenes[sceneIndex];
    if (!scene || !scene.dialogues) {
        app.closePreview();
        return;
    }

    state.currentDialogueIndex++;

    if (state.currentDialogueIndex >= scene.dialogues.length) {
        if (sceneIndex < state.scenes.length - 1) {
            state.history.push({ sceneIndex: sceneIndex, dialogueIndex: state.currentDialogueIndex - 1 });
            state.currentSceneIndex = sceneIndex + 1;
            state.currentDialogueIndex = -1;
            app.advancePreview();
        } else {
            app.showPreviewEnd();
        }
        return;
    }

    state.history.push({ sceneIndex: sceneIndex, dialogueIndex: state.currentDialogueIndex });

    var dialogue = scene.dialogues[state.currentDialogueIndex];
    app.renderPreviewDialogue(dialogue, scene);
};

app.goBackPreview = function () {
    var state = app.previewState;
    if (!state.scenes || state.scenes.length === 0) return;

    if (state.history.length <= 1) {
        return;
    }

    state.history.pop();
    var prev = state.history[state.history.length - 1];
    state.currentSceneIndex = prev.sceneIndex;
    state.currentDialogueIndex = prev.dialogueIndex;

    var scene = state.scenes[state.currentSceneIndex];
    var dialogue = scene.dialogues[state.currentDialogueIndex];
    app.renderPreviewDialogue(dialogue, scene);
};

app.renderPreviewDialogue = function (dialogue, scene) {
    var dialogueType = dialogue.type;
    if (typeof dialogueType === 'number') {
        dialogueType = ['Dialogue', 'Branch', 'Event'][dialogueType] || 'Dialogue';
    }

    var bgEl = document.getElementById('previewBg');
    var charEl = document.getElementById('previewChar');
    var dialogueEl = document.getElementById('previewDialogue');
    var speakerEl = document.getElementById('previewSpeaker');
    var textEl = document.getElementById('previewText');
    var choicesEl = document.getElementById('previewChoices');

    if (dialogueType === 'Dialogue') {
        var bg = dialogue.background || scene.background || '';
        if (bg && bg !== app.previewState.currentBg) {
            // 安全检查：确保背景值是纯文件名而非角色路径，过滤 BG_ 开头的脏数据
            var isPureFilename = bg.indexOf('/') === -1 && bg.indexOf('\\') === -1;
            var isNotSpriteLike = !/^BG_/i.test(bg);
            if (isPureFilename && isNotSpriteLike) {
                app.previewState.currentBg = bg;
                if (bgEl) {
                    bgEl.src = '/api/resources/file/Assets/Backgrounds/' + bg;
                    bgEl.style.display = 'block';
                }
            }
        }

        var spritePath = '';
        if (dialogue.sprites && dialogue.sprites.length > 0) {
            spritePath = dialogue.sprites[0].path || '';
        }
        // 安全检查：过滤以 BG_ 开头的背景图文件名被误作为角色立绘加载
        if (spritePath && spritePath !== app.previewState.currentChar && !/^BG_/i.test(spritePath)) {
            app.previewState.currentChar = spritePath;
            if (charEl) {
                charEl.src = '/api/resources/file/Assets/Characters/' + spritePath;
                charEl.style.display = 'block';
            }
        } else if (!spritePath) {
            app.previewState.currentChar = '';
            if (charEl) charEl.style.display = 'none';
        }

        if (dialogueEl) dialogueEl.style.display = 'block';
        if (speakerEl) speakerEl.textContent = dialogue.speaker || '';
        if (textEl) textEl.textContent = dialogue.text || '';
        if (choicesEl) choicesEl.style.display = 'none';

    } else if (dialogueType === 'Branch') {
        if (dialogueEl) dialogueEl.style.display = 'none';
        if (choicesEl) {
            choicesEl.style.display = 'block';
            choicesEl.innerHTML = '';

            var choices = (dialogue.branch && dialogue.branch.choices) || [];
            for (var i = 0; i < choices.length; i++) {
                (function (choice) {
                    var btn = document.createElement('button');
                    btn.className = 'preview-choice-btn';
                    btn.textContent = choice.text || '(未命名选项)';
                    btn.onclick = function () {
                        if (choice.targetScene) {
                            var targetIndex = app.findSceneIndex(choice.targetScene);
                            if (targetIndex >= 0) {
                                app.previewState.currentSceneIndex = targetIndex;
                                app.previewState.currentDialogueIndex = -1;
                                choicesEl.style.display = 'none';
                                app.advancePreview();
                            }
                        } else {
                            choicesEl.style.display = 'none';
                            app.advancePreview();
                        }
                    };
                    choicesEl.appendChild(btn);
                })(choices[i]);
            }
        }

    } else if (dialogueType === 'Event') {
        app.processPreviewEvent(dialogue);
        app.advancePreview();
    }
};

app.processPreviewEvent = function (dialogue) {
    if (!dialogue.event) return;

    var evt = dialogue.event;
    var eventType = evt.eventType;
    if (typeof eventType === 'number') {
        eventType = ['JumpScene', 'SetVariable', 'PlaySound', 'ChangeBackground', 'ChangeBgm', 'ShowCharacter', 'HideCharacter', 'Pause', 'WaitSeconds', 'WindowEffect', 'Custom', 'SendSystemNotification'][eventType] || 'Custom';
    }
    var params = evt.parameters || {};

    switch (eventType) {
        case 'SendSystemNotification':
            if (params.title || params.message) {
                app.fetchJson('/api/system/send-notification', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ title: params.title || '', message: params.message || '' })
                }).catch(function (err) {
                    console.warn('[Preview] 系统通知发送失败: ' + err.message);
                });
            }
            break;

        case 'ChangeBackground':
            if (params.background) {
                app.previewState.currentBg = params.background;
                var bgEl = document.getElementById('previewBg');
                if (bgEl) {
                    bgEl.src = '/api/resources/file/Assets/Backgrounds/' + params.background;
                    bgEl.style.display = 'block';
                }
            }
            break;

        case 'ChangeBgm':
            if (params.bgmFile) {
                var newBgm = params.bgmFile;
                // 循环解码 HTML 实体，处理可能的多重编码累积问题
                var decodedBgm = newBgm;
                var prev;
                var maxIterations = 10; // 防止无限循环
                do {
                    prev = decodedBgm;
                    decodedBgm = decodedBgm
                        .replace(/&apos;/g, "'")
                        .replace(/&quot;/g, '"')
                        .replace(/&amp;/g, '&')
                        .replace(/&lt;/g, '<')
                        .replace(/&gt;/g, '>');
                    maxIterations--;
                } while (decodedBgm !== prev && maxIterations > 0);

                console.log('[BGM] ChangeBgm 事件处理: raw="' + newBgm
                    + '" → decoded="' + decodedBgm
                    + '" | currentBgm="' + app.previewState.currentBgm + '"');

                // 仅在 BGM 真正变化时才切换播放，避免重复操作
                if (decodedBgm !== app.previewState.currentBgm) {
                    app.previewState.currentBgm = decodedBgm;
                    var bgmAudio = document.getElementById('previewBgm');
                    if (bgmAudio) {
                        var bgmUrl = '/api/resources/file/Assets/Musics/' + encodeURIComponent(decodedBgm);
                        console.log('[BGM] 切换背景音乐: ' + decodedBgm + ' | URL: ' + bgmUrl);
                        // 淡出当前 BGM → 切换资源 → 淡入新 BGM
                        var fadeOut = function () {
                            if (bgmAudio.volume > 0.05) {
                                bgmAudio.volume -= 0.05;
                                setTimeout(fadeOut, 30);
                            } else {
                                bgmAudio.pause();
                                bgmAudio.currentTime = 0;
                                bgmAudio.src = bgmUrl;
                                bgmAudio.volume = 0;
                                var fadeIn = function () {
                                    bgmAudio.play().catch(function (err) {
                                        console.warn('[BGM] 播放失败: ' + err.message);
                                    });
                                    if (bgmAudio.volume < 0.8) {
                                        bgmAudio.volume += 0.05;
                                        setTimeout(fadeIn, 30);
                                    }
                                };
                                fadeIn();
                            }
                        };
                        fadeOut();
                    }
                } else {
                    console.log('[BGM] BGM 未变化，跳过: ' + decodedBgm);
                }
            } else {
                // bgmFile 为空 → 停止 BGM 播放（对应选择"(无)"的情况）
                var stopAudio = document.getElementById('previewBgm');
                if (stopAudio) {
                    stopAudio.pause();
                    stopAudio.src = '';
                }
                app.previewState.currentBgm = '';
            }
            break;

        case 'ShowCharacter':
            if (params.character) {
                app.previewState.currentChar = params.character;
                var charEl = document.getElementById('previewChar');
                if (charEl) {
                    charEl.src = '/api/resources/file/Assets/Characters/' + params.character;
                    charEl.style.display = 'block';
                }
            }
            break;

        case 'HideCharacter':
            app.previewState.currentChar = '';
            var charEl = document.getElementById('previewChar');
            if (charEl) charEl.style.display = 'none';
            break;

        case 'JumpScene':
            if (params.targetScene) {
                var targetIndex = app.findSceneIndex(params.targetScene);
                if (targetIndex >= 0) {
                    app.previewState.currentSceneIndex = targetIndex;
                    app.previewState.currentDialogueIndex = -1;
                }
            }
            break;
    }
};

app.findSceneIndex = function (sceneId) {
    if (!app.previewState.scenes) return -1;
    for (var i = 0; i < app.previewState.scenes.length; i++) {
        if (app.previewState.scenes[i].id === sceneId) {
            return i;
        }
    }
    return -1;
};

app.showPreviewEnd = function () {
    var dialogueEl = document.getElementById('previewDialogue');
    var choicesEl = document.getElementById('previewChoices');
    if (dialogueEl) {
        dialogueEl.style.display = 'block';
        var speakerEl = document.getElementById('previewSpeaker');
        var textEl = document.getElementById('previewText');
        if (speakerEl) speakerEl.textContent = '';
        if (textEl) textEl.textContent = '— 预览结束 —';
    }
    if (choicesEl) choicesEl.style.display = 'none';
};

app.resetPreviewState = function () {
    app.previewState.scenes = null;
    app.previewState.projectPath = '';
    app.previewState.currentSceneIndex = 0;
    app.previewState.currentDialogueIndex = -1;
    app.previewState.history = [];
    app.previewState.currentBg = '';
    app.previewState.currentChar = '';
    app.previewState.currentBgm = '';

    // 停止 BGM 播放并释放音频资源
    var bgmAudio = document.getElementById('previewBgm');
    if (bgmAudio) {
        bgmAudio.pause();
        bgmAudio.src = '';
    }

    var bgEl = document.getElementById('previewBg');
    var charEl = document.getElementById('previewChar');
    var dialogueEl = document.getElementById('previewDialogue');
    var choicesEl = document.getElementById('previewChoices');

    if (bgEl) { bgEl.src = ''; bgEl.style.display = 'block'; }
    if (charEl) { charEl.src = ''; charEl.style.display = 'none'; }
    if (dialogueEl) dialogueEl.style.display = 'none';
    if (choicesEl) choicesEl.style.display = 'none';
};
