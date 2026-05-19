var app = app || {};

app.normalizeDialogueType = function (type) {
    var typeMap = { 0: 'Dialogue', 1: 'Branch', 2: 'Event' };
    if (typeMap[type]) return typeMap[type];
    return type;
};

app.normalizeEventType = function (eventType) {
    var eventTypeMap = {
        0: 'JumpScene', 1: 'SetVariable', 2: 'PlaySound',
        3: 'ChangeBackground', 4: 'ChangeBgm', 5: 'ShowCharacter',
        6: 'HideCharacter', 7: 'Pause', 8: 'WaitSeconds',
        9: 'WindowEffect', 10: 'Custom', 11: 'SendSystemNotification'
    };
    if (eventTypeMap[eventType]) return eventTypeMap[eventType];
    return eventType || 'Custom';
};

app.updateDialogueList = function () {
    var container = document.getElementById('dialogueList');
    if (!container) return;

    container.innerHTML = '';

    if (!app.state.project || !app.state.project.scenes) {
        container.innerHTML = '<p style="color: #666; text-align: center; margin-top: 40px;">请先新建或打开项目</p>';
        return;
    }

    var sceneIndex = app.state.activeSceneIndex;
    if (sceneIndex < 0 || sceneIndex >= app.state.project.scenes.length) {
        container.innerHTML = '<p style="color: #666; text-align: center; margin-top: 40px;">请选择一个场景</p>';
        return;
    }

    var scene = app.state.project.scenes[sceneIndex];
    if (!scene || !scene.dialogues || scene.dialogues.length === 0) {
        container.innerHTML = '<p style="color: #666; text-align: center; margin-top: 40px;">当前场景没有对话</p>';
        return;
    }

    var dialogues = scene.dialogues;
    for (var i = 0; i < dialogues.length; i++) {
        (function (index) {
            var dialogue = dialogues[index];
            var card = document.createElement('div');
            card.className = 'dialogue-card';
            if (index === app.state.selectedDialogueIndex) {
                card.classList.add('selected');
            }

            var normalizedType = app.normalizeDialogueType(dialogue.type);
            var typePrefix = '';
            if (normalizedType === 'Branch') {
                typePrefix = '<span class="type-indicator">[分支]</span> ';
            } else if (normalizedType === 'Event') {
                var evtType = app.normalizeEventType(dialogue.event && dialogue.event.eventType);
                typePrefix = '<span class="type-indicator">[事件:' + evtType + ']</span> ';
            }

            var speakerHtml = '';
            if (dialogue.speaker) {
                speakerHtml = '<div class="speaker">' + typePrefix + app.escapeHtml(dialogue.speaker) + '</div>';
            } else {
                speakerHtml = '<div class="speaker">' + typePrefix + '</div>';
            }

            var textPreview = dialogue.text || '';
            if (textPreview.length > 100) {
                textPreview = textPreview.substring(0, 100) + '...';
            }

            var textHtml = '<div class="dialogue-text">' + app.escapeHtml(textPreview) + '</div>';

            var choicesHtml = '';
            if (normalizedType === 'Branch' && dialogue.branch && dialogue.branch.choices && dialogue.branch.choices.length > 0) {
                var choiceTexts = [];
                for (var c = 0; c < dialogue.branch.choices.length; c++) {
                    choiceTexts.push(dialogue.branch.choices[c].text || '(未命名)');
                }
                choicesHtml = '<div class="dialogue-text" style="color: #10B981; margin-top: 4px;">选项: ' + app.escapeHtml(choiceTexts.join(' | ')) + '</div>';
            }

            card.innerHTML = speakerHtml + textHtml + choicesHtml;

            card.onclick = function () {
                app.selectDialogue(index);
            };

            container.appendChild(card);
        })(i);
    }
};

app.escapeHtml = function (text) {
    if (!text) return '';
    var div = document.createElement('div');
    div.appendChild(document.createTextNode(text));
    return div.innerHTML;
};
