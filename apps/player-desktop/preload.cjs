const { contextBridge } = require('electron')

contextBridge.exposeInMainWorld('visunoviaPlayer', {
  platform: 'electron',
})
