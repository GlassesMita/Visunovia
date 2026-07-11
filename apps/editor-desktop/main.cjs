const { app, BrowserWindow, dialog, ipcMain, Menu, protocol, screen, shell } = require('electron')
const http = require('node:http')
const fs = require('node:fs/promises')
const path = require('node:path')
const { handleBackendRequest, createBackendState } = require('./backend.cjs')
const { startFrontendServer, getContentType } = require('./server.cjs')

const isDevelopment = process.argv.includes('--dev')
let frontendServer = null
const backendState = createBackendState()
let frontendStaticRoot = null
let backendBaseUrl = ''
let clientEntry = null
let windowStateSaveTimer = null
let mainWindow = null
let welcomeWindow = null
let isQuitting = false
const consoleEntries = []
const MAX_CONSOLE_ENTRIES = 2000
const MAX_STAGE_WINDOWS = 8
const stageWindows = new Map()

const defaultMainWindowState = {
  width: 1440,
  height: 960,
}

function getWindowStatePath() {
  return path.join(app.getPath('userData'), 'window-state.json')
}

function isVisibleOnAnyDisplay(bounds) {
  return screen.getAllDisplays().some(({ workArea }) => {
    const overlapWidth = Math.min(bounds.x + bounds.width, workArea.x + workArea.width) - Math.max(bounds.x, workArea.x)
    const overlapHeight = Math.min(bounds.y + bounds.height, workArea.y + workArea.height) - Math.max(bounds.y, workArea.y)
    return overlapWidth >= 100 && overlapHeight >= 100
  })
}

async function loadMainWindowState() {
  try {
    const state = JSON.parse(await fs.readFile(getWindowStatePath(), 'utf8'))
    const bounds = {
      x: Number(state.x),
      y: Number(state.y),
      width: Math.max(1120, Number(state.width)),
      height: Math.max(720, Number(state.height)),
    }
    if (Object.values(bounds).every(Number.isFinite) && isVisibleOnAnyDisplay(bounds)) {
      return { ...bounds, isMaximized: Boolean(state.isMaximized) }
    }
  } catch {
    // Use the default centered bounds when no valid state is available.
  }
  return defaultMainWindowState
}

async function saveMainWindowState(mainWindow) {
  if (mainWindow.isDestroyed() || mainWindow.isMinimized()) return
  const bounds = mainWindow.isMaximized() ? mainWindow.getNormalBounds() : mainWindow.getBounds()
  const state = { ...bounds, isMaximized: mainWindow.isMaximized() }
  await fs.mkdir(path.dirname(getWindowStatePath()), { recursive: true })
  await fs.writeFile(getWindowStatePath(), JSON.stringify(state, null, 2), 'utf8')
}

function scheduleMainWindowStateSave(mainWindow) {
  clearTimeout(windowStateSaveTimer)
  windowStateSaveTimer = setTimeout(() => {
    saveMainWindowState(mainWindow).catch(error => console.warn('Failed to save window state:', error))
  }, 250)
}

function getPopupOptions(url, mainWindow) {
  const parsedUrl = new URL(url, 'http://localhost')
  const route = parsedUrl.hash.replace(/^#/, '') || parsedUrl.pathname
  const dimensions = route.toLowerCase() === '/preferences'
    ? { width: 800, height: 600 }
    : route.toLowerCase() === '/about'
      ? { width: 720, height: 560 }
      : route.toLowerCase() === '/console'
        ? { width: 960, height: 600 }
      : null
  if (!dimensions) return null

  const { workArea } = screen.getDisplayMatching(mainWindow.getBounds())
  return {
    ...dimensions,
    x: Math.round(workArea.x + (workArea.width - dimensions.width) / 2),
    y: Math.round(workArea.y + (workArea.height - dimensions.height) / 2),
    resizable: route.toLowerCase() === '/console',
    maximizable: route.toLowerCase() === '/console',
    fullscreenable: false,
  }
}

function normalizeStageWindowId(value) {
  const id = Number(value)
  if (!Number.isInteger(id) || id < 1 || id > MAX_STAGE_WINDOWS) {
    throw new Error(`Stage window ID must be an integer from 1 to ${MAX_STAGE_WINDOWS}`)
  }
  return id
}

function getStageWindow(id) {
  const window = stageWindows.get(id)
  return window && !window.isDestroyed() ? window : null
}

async function createStageWindow(id, command) {
  const existing = getStageWindow(id)
  if (existing) return existing

  const width = Math.max(320, Math.round(Number(command.width) || 1280))
  const height = Math.max(180, Math.round(Number(command.height) || 720))
  const window = new BrowserWindow({
    width,
    height,
    x: Number.isFinite(Number(command.x)) ? Math.round(Number(command.x)) : undefined,
    y: Number.isFinite(Number(command.y)) ? Math.round(Number(command.y)) : undefined,
    show: false,
    autoHideMenuBar: true,
    webPreferences: {
      preload: path.join(__dirname, 'preload.cjs'),
      contextIsolation: true,
      nodeIntegration: false,
    },
  })
  window.setMenu(null)
  window.on('closed', () => stageWindows.delete(id))
  stageWindows.set(id, window)

  const entry = await resolveClientEntry()
  const url = new URL(entry.value)
  url.searchParams.set('stageWindow', String(id))
  await window.loadURL(url.toString())
  return window
}

function applyWindowBounds(window, command) {
  const bounds = window.getBounds()
  const nextBounds = {
    x: Number.isFinite(Number(command.x)) ? Math.round(Number(command.x)) : bounds.x,
    y: Number.isFinite(Number(command.y)) ? Math.round(Number(command.y)) : bounds.y,
    width: Number.isFinite(Number(command.width)) ? Math.max(320, Math.round(Number(command.width))) : bounds.width,
    height: Number.isFinite(Number(command.height)) ? Math.max(180, Math.round(Number(command.height))) : bounds.height,
  }
  window.setBounds(nextBounds)
}

function appendConsoleEntry(entry) {
  const normalizedEntry = {
    id: `${Date.now()}-${Math.random().toString(36).slice(2)}`,
    timestamp: entry?.timestamp || new Date().toISOString(),
    level: ['debug', 'info', 'success', 'warning', 'error'].includes(entry?.level) ? entry.level : 'info',
    source: String(entry?.source || 'Editor'),
    message: String(entry?.message || ''),
  }
  consoleEntries.push(normalizedEntry)
  if (consoleEntries.length > MAX_CONSOLE_ENTRIES) {
    consoleEntries.splice(0, consoleEntries.length - MAX_CONSOLE_ENTRIES)
  }
  for (const window of BrowserWindow.getAllWindows()) {
    if (!window.isDestroyed()) window.webContents.send('console:entry', normalizedEntry)
  }
  return normalizedEntry
}

protocol.registerSchemesAsPrivileged([
  {
    scheme: 'visunovia',
    privileges: {
      standard: true,
      secure: true,
      supportFetchAPI: true,
      corsEnabled: true,
    },
  },
])

function repoRoot() {
  return path.resolve(__dirname, '..', '..')
}

function canReach(url) {
  return new Promise((resolve) => {
    const request = http.get(url, (response) => {
      response.resume()
      resolve(response.statusCode >= 200 && response.statusCode < 500)
    })
    request.once('error', () => resolve(false))
    request.setTimeout(1000, () => {
      request.destroy()
      resolve(false)
    })
  })
}

async function resolveClientEntry() {
  if (clientEntry) return clientEntry
  const staticRoot = path.join(repoRoot(), 'www_build')
  frontendServer = await startFrontendServer({ port: isDevelopment ? 32523 : 0, staticRoot, app, state: backendState })
  frontendStaticRoot = staticRoot
  backendBaseUrl = frontendServer.url.replace(/\/$/, '')

  if (isDevelopment) {
    const devServerUrl = 'http://127.0.0.1:32423/'
    if (await canReach(devServerUrl)) {
      clientEntry = { kind: 'url', value: devServerUrl }
      return clientEntry
    }
  }

  clientEntry = { kind: 'url', value: 'visunovia://app/' }
  return clientEntry
}

function resolveStaticProtocolPath(url) {
  const parsed = new URL(url)
  const pathname = decodeURIComponent(parsed.pathname || '/index.html')
  const relativePath = pathname.replace(/^\/+/, '') || 'index.html'
  const root = path.resolve(frontendStaticRoot || path.join(repoRoot(), 'www_build'))
  let targetPath = path.resolve(root, relativePath)
  if (!targetPath.startsWith(root)) {
    targetPath = path.join(root, 'index.html')
  }
  return { root, targetPath }
}

function registerStaticProtocol() {
  protocol.handle('visunovia', async (request) => {
    const { root, targetPath } = resolveStaticProtocolPath(request.url)
    try {
      const stat = await fs.stat(targetPath)
      const filePath = stat.isDirectory() ? path.join(targetPath, 'index.html') : targetPath
      const buffer = await fs.readFile(filePath)
      return new Response(buffer, {
        headers: {
          'Content-Type': getContentType(filePath),
          'Cache-Control': 'no-cache',
        },
      })
    } catch {
      const fallbackPath = path.join(root, 'index.html')
      const buffer = await fs.readFile(fallbackPath)
      return new Response(buffer, {
        headers: {
          'Content-Type': 'text/html; charset=utf-8',
          'Cache-Control': 'no-cache',
        },
      })
    }
  })
}

async function createMainWindow(projectPath = '') {
  if (mainWindow && !mainWindow.isDestroyed()) {
    if (projectPath) {
      const entry = await resolveClientEntry()
      void mainWindow.loadURL(buildClientUrl(entry, '/', projectPath)).catch(error => {
        console.error('Failed to load project in editor window:', error)
      })
    }
    mainWindow.show()
    mainWindow.focus()
    return mainWindow
  }
  const savedWindowState = await loadMainWindowState()
  mainWindow = new BrowserWindow({
    ...savedWindowState,
    minWidth: 1120,
    minHeight: 720,
    show: false,
    autoHideMenuBar: true,
    webPreferences: {
      preload: path.join(__dirname, 'preload.cjs'),
      contextIsolation: true,
      nodeIntegration: false,
    },
  })
  mainWindow.setMenu(null)
  if (savedWindowState.isMaximized) {
    mainWindow.maximize()
  }
  mainWindow.on('resize', () => scheduleMainWindowStateSave(mainWindow))
  mainWindow.on('move', () => scheduleMainWindowStateSave(mainWindow))
  mainWindow.on('close', () => {
    clearTimeout(windowStateSaveTimer)
    saveMainWindowState(mainWindow).catch(error => console.warn('Failed to save window state:', error))
  })

  mainWindow.once('ready-to-show', () => {
    if (!mainWindow?.isDestroyed() && !mainWindow.isVisible()) {
      mainWindow.show()
    }
  })

  const entry = await resolveClientEntry()
  void mainWindow.loadURL(buildClientUrl(entry, '/', projectPath)).catch(error => {
    console.error('Failed to load editor window:', error)
  })

  // 为 window.open() 创建的弹窗窗口配置 preload 脚本
  mainWindow.webContents.setWindowOpenHandler(({ url }) => {
    const popupOptions = getPopupOptions(url, mainWindow)
    return {
      action: 'allow',
      overrideBrowserWindowOptions: {
        ...(popupOptions || {}),
        webPreferences: {
          preload: path.join(__dirname, 'preload.cjs'),
          contextIsolation: true,
          nodeIntegration: false,
        },
      },
    }
  })
  mainWindow.on('closed', () => {
    mainWindow = null
    if (!isQuitting && (!welcomeWindow || welcomeWindow.isDestroyed())) {
      void createWelcomeWindow()
    }
  })
  return mainWindow
}

function buildClientUrl(entry, route = '/', projectPath = '') {
  const url = new URL(entry.value)
  if (projectPath) url.searchParams.set('project', projectPath)
  if (entry.value.startsWith('visunovia:')) {
    url.hash = route === '/' ? '/' : route
  } else {
    url.pathname = route
  }
  return url.toString()
}

async function createWelcomeWindow() {
  if (welcomeWindow && !welcomeWindow.isDestroyed()) {
    welcomeWindow.show()
    welcomeWindow.focus()
    return welcomeWindow
  }
  const entry = await resolveClientEntry()
  welcomeWindow = new BrowserWindow({
    width: 820,
    height: 520,
    minWidth: 720,
    minHeight: 460,
    show: false,
    autoHideMenuBar: true,
    resizable: false,
    webPreferences: {
      preload: path.join(__dirname, 'preload.cjs'),
      contextIsolation: true,
      nodeIntegration: false,
    },
  })
  welcomeWindow.setMenu(null)
  welcomeWindow.once('ready-to-show', () => welcomeWindow?.show())
  welcomeWindow.on('closed', () => {
    welcomeWindow = null
    if (!mainWindow || mainWindow.isDestroyed()) app.quit()
  })
  await welcomeWindow.loadURL(buildClientUrl(entry, '/Welcome'))
  return welcomeWindow
}

async function openProjectInMainWindow(projectPath) {
  const editor = await createMainWindow(projectPath)
  editor.once('ready-to-show', () => editor.focus())
  if (welcomeWindow && !welcomeWindow.isDestroyed()) welcomeWindow.close()
}

ipcMain.handle('backend:request', async (_event, request) => {
  const result = await handleBackendRequest(request, app, backendState)
  if (result.status >= 400) {
    appendConsoleEntry({
      level: 'error',
      source: 'Node Backend',
      message: `${request.method || 'GET'} ${request.url}: ${result.data?.error || result.status}`,
    })
  } else if (request.method === 'PUT' && String(request.url || '').startsWith('/scenegraphs/')) {
    appendConsoleEntry({ level: 'success', source: 'Node Backend', message: `Scene saved: ${decodeURIComponent(String(request.url).split('/').pop() || '')}` })
  }
  return result
})
ipcMain.handle('console:getEntries', () => consoleEntries)
ipcMain.handle('console:clear', () => {
  consoleEntries.length = 0
  for (const window of BrowserWindow.getAllWindows()) {
    if (!window.isDestroyed()) window.webContents.send('console:cleared')
  }
})
ipcMain.on('console:append', (_event, entry) => appendConsoleEntry(entry))
ipcMain.handle('backend:getBaseUrl', () => backendBaseUrl)
ipcMain.handle('dialog:open', async (event, options = {}) => {
  const owner = BrowserWindow.fromWebContents(event.sender)
  const selectDirectory = options.kind === 'directory'
  const extensions = Array.isArray(options.extensions)
    ? options.extensions.map(extension => String(extension).replace(/^\./, '')).filter(Boolean)
    : []
  const result = await dialog.showOpenDialog(owner, {
    title: typeof options.title === 'string' ? options.title : undefined,
    defaultPath: typeof options.defaultPath === 'string' ? options.defaultPath : undefined,
    properties: selectDirectory ? ['openDirectory', 'createDirectory'] : ['openFile'],
    filters: selectDirectory || extensions.length === 0
      ? undefined
      : [{ name: options.filterName || 'Supported files', extensions }, { name: 'All files', extensions: ['*'] }],
  })
  return result.canceled ? null : result.filePaths[0] || null
})
ipcMain.handle('shell:openExternal', async (_event, url) => {
  const parsedUrl = new URL(url)
  if (parsedUrl.protocol !== 'https:') {
    throw new Error('Only HTTPS external links are allowed')
  }
  await shell.openExternal(parsedUrl.toString())
})
ipcMain.handle('window:setTitle', (event, title) => {
  const window = BrowserWindow.fromWebContents(event.sender)
  if (window && typeof title === 'string') {
    window.setTitle(title)
  }
})
ipcMain.handle('welcome:openProject', async (_event, projectPath) => {
  const normalizedPath = String(projectPath || '').trim()
  if (!normalizedPath) throw new Error('Project path is required')
  await openProjectInMainWindow(normalizedPath)
})
ipcMain.handle('welcome:openEditor', async () => {
  const editor = await createMainWindow()
  editor.show()
  editor.focus()
  if (welcomeWindow && !welcomeWindow.isDestroyed()) welcomeWindow.close()
})
ipcMain.handle('stage:window-command', async (_event, command = {}) => {
  const type = String(command.type || '')
  const id = normalizeStageWindowId(command.targetWindow)
  let window = getStageWindow(id)

  if (type === 'createWindow') {
    await createStageWindow(id, command)
    return
  }
  if (!window) throw new Error(`Stage window ${id} does not exist`)

  if (type === 'closeWindow') {
    window.close()
  } else if (type === 'showWindow') {
    window.show()
    window.focus()
  } else if (type === 'hideWindow') {
    window.hide()
  } else if (type === 'moveWindow' || type === 'resizeWindow') {
    applyWindowBounds(window, command)
  } else if (type === 'setWindowAlwaysOnTop') {
    window.setAlwaysOnTop(Boolean(command.alwaysOnTop))
  } else {
    throw new Error(`Unsupported stage window command: ${type}`)
  }
})
ipcMain.handle('stage:extension-event', (_event, payload = {}) => {
  const event = {
    name: String(payload.name || ''),
    args: Array.isArray(payload.args) ? payload.args : [],
  }
  if (!event.name) throw new Error('Extension event name is required')
  for (const window of stageWindows.values()) {
    if (!window.isDestroyed()) window.webContents.send('stage:extension-event', event)
  }
})

app.whenReady().then(() => {
  Menu.setApplicationMenu(null)
  registerStaticProtocol()
  return createWelcomeWindow()
})

app.on('window-all-closed', () => {
  if (frontendServer) frontendServer.close()
  if (process.platform !== 'darwin') app.quit()
})

app.on('before-quit', () => {
  isQuitting = true
})

app.on('activate', () => {
  if (BrowserWindow.getAllWindows().length === 0) createWelcomeWindow()
})
