var app = app || {};

app.previewState = {
    scenes: null,
    projectPath: '',
    currentSceneIndex: 0,
    currentDialogueIndex: -1,
    history: [],
    currentBg: '',
    currentChar: '',
    currentCharSlots: {},
    currentBgm: ''
};

app.previewVideoBackgroundExtensions = ['.mp4', '.webm', '.m4v', '.mov', '.ogv'];

app.previewPopupWindow = null;
app.previewPopupSize = { width: 1920, height: 1080 };

app.openPreviewPopup = function (width, height) {
    width = Number(width) || 1920;
    height = Number(height) || 1080;
    app.previewPopupSize = { width: width, height: height };

    var features = [
        'popup=yes',
        'width=' + width,
        'height=' + height,
        'resizable=yes',
        'scrollbars=no'
    ].join(',');

    var popup = window.open('', 'VisunoviaPreviewPopup', features);
    if (!popup) {
        app.setStatus('弹窗被浏览器拦截，请允许弹出窗口后重试');
        return;
    }

    app.previewPopupWindow = popup;
    app.writePreviewPopupDocument(popup, width, height);
    app.syncPreviewPopup();
    popup.focus();
};

app.writePreviewPopupDocument = function (popup, width, height) {
    popup.document.open();
    popup.document.write('<!doctype html>'
        + '<html lang="zh-CN">'
        + '<head>'
        + '<meta charset="utf-8">'
        + '<meta name="viewport" content="width=device-width, initial-scale=1">'
        + '<title>Visunovia 预览</title>'
        + '<link rel="stylesheet" href="/css/editor.css">'
        + '<style>'
        + 'html,body{width:100%;height:100%;margin:0;overflow:hidden;background:#000;}'
        + '.preview-overlay{display:block;position:fixed;inset:0;width:100vw;height:100vh;background:#000;}'
        + '.preview-controls,.preview-close{display:none!important;}'
        + '.preview-stage-size{position:fixed;inset:0;width:100vw;height:100vh;overflow:hidden;background:#000;}'
        + '</style>'
        + '</head>'
        + '<body>'
        + '<div class="preview-stage-size" style="aspect-ratio:' + width + '/' + height + '">'
        + '<div class="preview-overlay visible" id="previewPopupStage">'
        + '<img class="preview-bg" id="popupPreviewBg" alt="" />'
        + '<video class="preview-bg" id="popupPreviewBgVideo" autoplay loop muted playsinline preload="auto" style="display:none;"></video>'
        + '<img class="preview-char" id="popupPreviewChar" alt="" style="display:none;" />'
        + '<div class="preview-dialogue" id="popupPreviewDialogue" style="display:none;">'
        + '<div class="preview-speaker" id="popupPreviewSpeaker"></div>'
        + '<div class="preview-text" id="popupPreviewText"></div>'
        + '</div>'
        + '<div class="preview-choices" id="popupPreviewChoices" style="display:none;"></div>'
        + '</div>'
        + '</div>'
        + '</body>'
        + '</html>');
    popup.document.close();
};

app.syncPreviewPopup = function () {
    var popup = app.previewPopupWindow;
    if (!popup || popup.closed || !popup.document) return;

    var sourceImageBg = document.getElementById('previewBg');
    var sourceVideoBg = document.getElementById('previewBgVideo');
    var sourceChar = document.getElementById('previewChar');
    var sourceDialogue = document.getElementById('previewDialogue');
    var sourceSpeaker = document.getElementById('previewSpeaker');
    var sourceText = document.getElementById('previewText');
    var sourceChoices = document.getElementById('previewChoices');

    app.copyPreviewImageElement(sourceImageBg, popup.document.getElementById('popupPreviewBg'));
    app.copyPreviewVideoElement(sourceVideoBg, popup.document.getElementById('popupPreviewBgVideo'));
    app.copyPreviewImageElement(sourceChar, popup.document.getElementById('popupPreviewChar'));
    app.copyPreviewPanelElement(sourceDialogue, popup.document.getElementById('popupPreviewDialogue'));

    var popupSpeaker = popup.document.getElementById('popupPreviewSpeaker');
    var popupText = popup.document.getElementById('popupPreviewText');
    var popupChoices = popup.document.getElementById('popupPreviewChoices');

    if (popupSpeaker && sourceSpeaker) popupSpeaker.textContent = sourceSpeaker.textContent;
    if (popupText && sourceText) popupText.textContent = sourceText.textContent;
    if (popupChoices && sourceChoices) {
        popupChoices.style.display = sourceChoices.style.display;
        popupChoices.innerHTML = sourceChoices.innerHTML;
        var buttons = popupChoices.querySelectorAll('button');
        for (var i = 0; i < buttons.length; i++) {
            buttons[i].removeAttribute('onclick');
            buttons[i].disabled = true;
        }
    }
};

app.copyPreviewImageElement = function (source, target) {
    if (!source || !target) return;
    target.style.display = source.style.display;
    if (source.getAttribute('src')) {
        target.src = source.getAttribute('src');
    } else {
        target.removeAttribute('src');
    }
};

app.copyPreviewVideoElement = function (source, target) {
    if (!source || !target) return;
    target.style.display = source.style.display;
    if (source.getAttribute('src')) {
        if (target.getAttribute('src') !== source.getAttribute('src')) {
            target.src = source.getAttribute('src');
            target.load();
        }
        target.play().catch(function () { });
    } else {
        target.pause();
        target.removeAttribute('src');
        target.load();
    }
};

app.copyPreviewPanelElement = function (source, target) {
    if (!source || !target) return;
    target.style.display = source.style.display;
};

app.isPreviewVideoBackground = function (path) {
    var cleanPath = String(path || '').split(/[?#]/)[0].toLowerCase();
    for (var i = 0; i < app.previewVideoBackgroundExtensions.length; i++) {
        if (cleanPath.slice(-app.previewVideoBackgroundExtensions[i].length) === app.previewVideoBackgroundExtensions[i]) {
            return true;
        }
    }
    return false;
};

app.resolvePreviewBackgroundUrl = function (backgroundPath) {
    if (!backgroundPath) return '';
    if (/^[a-zA-Z]:[\\/]/.test(backgroundPath) || backgroundPath.indexOf('\\\\') === 0 || backgroundPath.charAt(0) === '/') {
        return '/api/FileBrowser/preview?path=' + encodeURIComponent(backgroundPath);
    }
    return '/api/resources/file/Assets/Backgrounds/' + encodeURIComponent(backgroundPath);
};

app.setPreviewBackground = function (backgroundPath) {
    var imageEl = document.getElementById('previewBg');
    var videoEl = document.getElementById('previewBgVideo');
    var url = app.resolvePreviewBackgroundUrl(backgroundPath);
    var isVideo = app.isPreviewVideoBackground(backgroundPath);

    if (imageEl) {
        imageEl.style.display = isVideo ? 'none' : 'block';
        if (isVideo) imageEl.removeAttribute('src');
    }

    if (videoEl) {
        videoEl.style.display = isVideo ? 'block' : 'none';
        if (!isVideo) {
            videoEl.pause();
            videoEl.removeAttribute('src');
            videoEl.load();
        }
    }

    if (isVideo && videoEl) {
        if (videoEl.src !== url) videoEl.src = url;
        videoEl.type = app.getPreviewVideoMimeType(backgroundPath);
        videoEl.muted = true;
        videoEl.loop = true;
        videoEl.playsInline = true;
        videoEl.play().catch(function (err) {
            console.warn('[Preview] 背景视频播放失败: ' + err.message);
        });
    } else if (imageEl) {
        imageEl.src = url;
    }
};

app.getPreviewVideoMimeType = function (path) {
    var cleanPath = String(path || '').split(/[?#]/)[0].toLowerCase();
    if (cleanPath.slice(-5) === '.webm') return 'video/webm';
    if (cleanPath.slice(-4) === '.mp4') return 'video/mp4';
    if (cleanPath.slice(-4) === '.m4v') return 'video/x-m4v';
    if (cleanPath.slice(-4) === '.mov') return 'video/quicktime';
    if (cleanPath.slice(-4) === '.ogv') return 'video/ogg';
    return 'video/webm';
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
    app.previewState.currentCharSlots = {};
    app.previewState.currentBgm = '';

    var overlay = document.getElementById('previewOverlay');
    if (overlay) {
        overlay.classList.add('visible');
    }

    app.advancePreview();
    app.syncPreviewPopup();
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
    app.syncPreviewPopup();
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
    app.syncPreviewPopup();
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
                app.setPreviewBackground(bg);
            }
        }

        var controls = dialogue.characterControls || dialogue.CharacterControls || [];
        if (controls && controls.length > 0) {
            app.applyPreviewCharacterControls(controls);
        }

        var spritePath = '';
        if (dialogue.sprites && dialogue.sprites.length > 0) {
            spritePath = dialogue.sprites[0].path || '';
        }
        // 安全检查：过滤以 BG_ 开头的背景图文件名被误作为角色立绘加载
        if (!controls || controls.length === 0 && spritePath && spritePath !== app.previewState.currentChar && !/^BG_/i.test(spritePath)) {
            app.previewState.currentChar = spritePath;
            if (charEl) {
                charEl.src = app.resolvePreviewSpriteUrl(spritePath);
                charEl.style.display = 'block';
            }
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

app.resolvePreviewSpriteUrl = function (spritePath) {
    if (!spritePath) return '';
    if (/^[a-zA-Z]:[\\/]/.test(spritePath) || spritePath.indexOf('\\\\') === 0 || spritePath.charAt(0) === '/') {
        return '/api/FileBrowser/preview?path=' + encodeURIComponent(spritePath);
    }
    return '/api/resources/file/Assets/Characters/' + spritePath;
};

app.resolvePreviewSfxUrl = function (soundPath) {
    if (!soundPath) return '';
    if (/^[a-zA-Z]:[\\/]/.test(soundPath) || soundPath.indexOf('\\\\') === 0 || soundPath.charAt(0) === '/') {
        return soundPath;
    }
    return '/api/resources/file/Assets/Sfx/' + encodeURIComponent(soundPath);
};

app.playPreviewSfx = function (soundPath) {
    if (!soundPath) return;
    var audio = new Audio(app.resolvePreviewSfxUrl(soundPath));
    audio.volume = 0.9;
    audio.play().catch(function (err) {
        console.warn('[Preview] 音效播放失败: ' + err.message);
    });
};

app.applyPreviewCharacterControls = function (controls) {
    var slots = app.previewState.currentCharSlots || {};
    app.previewState.currentCharSlots = slots;

    for (var i = 0; i < controls.length; i++) {
        var control = controls[i] || {};
        var slot = String(control.slot || control.Slot || (i + 1));
        var action = String(control.action || control.Action || 'show').toLowerCase();
        var sprite = control.sprite || control.Sprite || '';
        var sfx = control.sfx || control.Sfx || '';

        if (sfx) {
            app.playPreviewSfx(sfx);
        }

        if (action === 'hide') {
            delete slots[slot];
            continue;
        }

        slots[slot] = {
            character: control.character || control.Character || '',
            sprite: sprite || (slots[slot] && slots[slot].sprite) || '',
            position: control.position || control.Position || 'center',
            animation: control.animation || control.Animation || 'fade'
        };
    }

    app.renderPreviewCharacterSlots();
};

app.renderPreviewCharacterSlots = function () {
    var charEl = document.getElementById('previewChar');
    if (!charEl) return;

    var slots = app.previewState.currentCharSlots || {};
    var slotKeys = Object.keys(slots).sort(function (a, b) { return Number(a) - Number(b); });
    var active = null;
    for (var i = 0; i < slotKeys.length; i++) {
        var candidate = slots[slotKeys[i]];
        if (candidate && candidate.sprite) {
            active = candidate;
            break;
        }
    }

    if (!active) {
        app.previewState.currentChar = '';
        charEl.style.display = 'none';
        return;
    }

    if (active.sprite !== app.previewState.currentChar) {
        app.previewState.currentChar = active.sprite;
        charEl.src = app.resolvePreviewSpriteUrl(active.sprite);
    }
    charEl.style.display = 'block';
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
                app.setPreviewBackground(params.background);
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
                    charEl.src = app.resolvePreviewSpriteUrl(params.character);
                    charEl.style.display = 'block';
                }
            }
            break;

        case 'PlaySound':
            app.playPreviewSfx(params.soundFile || params.SoundFile || '');
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
    app.syncPreviewPopup();
};

app.resetPreviewState = function () {
    app.previewState.scenes = null;
    app.previewState.projectPath = '';
    app.previewState.currentSceneIndex = 0;
    app.previewState.currentDialogueIndex = -1;
    app.previewState.history = [];
    app.previewState.currentBg = '';
    app.previewState.currentChar = '';
    app.previewState.currentCharSlots = {};
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

    if (app.previewPopupWindow && !app.previewPopupWindow.closed) {
        app.previewPopupWindow.close();
    }
    app.previewPopupWindow = null;
};
