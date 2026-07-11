const { app, BrowserWindow } = require('electron')
const path = require('node:path')
const { startFrontendServer } = require('../editor-desktop/server.cjs')

const isDevelopment = process.argv.includes('--dev')
let frontendServer = null

function repoRoot() {
  return path.resolve(__dirname, '..', '..')
}

function getProjectArg() {
  const index = process.argv.findIndex(arg => arg === '--project')
  return index >= 0 ? process.argv[index + 1] : null
}

async function resolvePlayerUrl() {
  const projectPath = getProjectArg()
  if (isDevelopment) {
    const url = new URL('http://localhost:32423/')
    if (projectPath) url.searchParams.set('project', projectPath)
    return url.toString()
  }

  frontendServer = await startFrontendServer({
    port: 32524,
    staticRoot: path.join(repoRoot(), 'www_build'),
    app,
  })
  const url = new URL(frontendServer.url)
  if (projectPath) url.searchParams.set('project', projectPath)
  return url.toString()
}

async function createPlayerWindow() {
  const playerWindow = new BrowserWindow({
    width: 1280,
    height: 720,
    minWidth: 960,
    minHeight: 540,
    fullscreenable: true,
    webPreferences: {
      preload: path.join(__dirname, 'preload.cjs'),
      contextIsolation: true,
      nodeIntegration: false,
    },
  })

  await playerWindow.loadURL(await resolvePlayerUrl())
}

app.whenReady().then(createPlayerWindow)

app.on('window-all-closed', () => {
  if (frontendServer) frontendServer.close()
  if (process.platform !== 'darwin') app.quit()
})

app.on('activate', () => {
  if (BrowserWindow.getAllWindows().length === 0) createPlayerWindow()
})
