const { contextBridge, ipcRenderer } = require('electron')

contextBridge.exposeInMainWorld('visunoviaDesktop', {
  platform: 'electron',
  request: (request) => ipcRenderer.invoke('backend:request', request),
  getBackendBaseUrl: () => ipcRenderer.invoke('backend:getBaseUrl'),
  openDialog: (options) => ipcRenderer.invoke('dialog:open', options),
  openExternal: (url) => ipcRenderer.invoke('shell:openExternal', url),
  setWindowTitle: (title) => ipcRenderer.invoke('window:setTitle', title),
  openProjectFromWelcome: (projectPath) => ipcRenderer.invoke('welcome:openProject', projectPath),
  openEditorFromWelcome: () => ipcRenderer.invoke('welcome:openEditor'),
  executeStageCommand: (command) => ipcRenderer.invoke('stage:window-command', command),
  emitExtensionEvent: (event) => ipcRenderer.invoke('stage:extension-event', event),
  notifyReady: () => ipcRenderer.send('renderer:ready'),
  appendConsoleEntry: (entry) => ipcRenderer.send('console:append', entry),
  getConsoleEntries: () => ipcRenderer.invoke('console:getEntries'),
  clearConsoleEntries: () => ipcRenderer.invoke('console:clear'),
  onConsoleEntry: (listener) => {
    const handler = (_event, entry) => listener(entry)
    ipcRenderer.on('console:entry', handler)
    return () => ipcRenderer.removeListener('console:entry', handler)
  },
  onConsoleCleared: (listener) => {
    const handler = () => listener()
    ipcRenderer.on('console:cleared', handler)
    return () => ipcRenderer.removeListener('console:cleared', handler)
  },
})
