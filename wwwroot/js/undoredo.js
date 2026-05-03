var app = app || {};

app.pushUndo = function (action) {
    app.updateUndoRedoState();
};

app.updateUndoRedoState = function () {
    fetch('/api/editor/can-undo', { method: 'GET' })
        .then(function (res) { return res.json(); })
        .then(function (data) {
            var undoBtns = document.querySelectorAll('[onclick="app.undo()"]');
            var redoBtns = document.querySelectorAll('[onclick="app.redo()"]');

            for (var i = 0; i < undoBtns.length; i++) {
                undoBtns[i].disabled = !data.canUndo;
                undoBtns[i].style.opacity = data.canUndo ? '1' : '0.4';
            }

            for (var i = 0; i < redoBtns.length; i++) {
                redoBtns[i].disabled = !data.canRedo;
                redoBtns[i].style.opacity = data.canRedo ? '1' : '0.4';
            }
        })
        .catch(function () {
        });
};
