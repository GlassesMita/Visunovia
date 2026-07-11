const fs = require('node:fs/promises')
const os = require('node:os')
const path = require('node:path')
const { execFile } = require('node:child_process')
const { promisify } = require('node:util')

const execFileAsync = promisify(execFile)

const textExtensions = new Set(['.lrc', '.txt', '.lor', '.json', '.xml', '.md', '.po', '.csv', '.tsv', '.resona'])
const mediaExtensions = new Set(['.png', '.jpg', '.jpeg', '.gif', '.webp', '.bmp', '.svg', '.ico', '.tga', '.dds', '.mp4', '.webm', '.m4v', '.mov', '.ogv', '.mp3', '.wav', '.ogg', '.oga', '.flac', '.m4a', '.aac', '.opus', '.weba', '.mod', '.xm', '.s3m', '.it', '.mptm'])
const assetCategories = {
  sprites: ['Assets', 'Characters'],
  backgrounds: ['Assets', 'Backgrounds'],
  bgm: ['Assets', 'Musics'],
  voice: ['Assets', 'Voices'],
  sfx: ['Assets', 'Sfx'],
}
const defaultSettings = {
  Language: 'zh-CN',
  Theme: 'system',
  ThemeStyle: 'material-you',
  PlaceholderCompanyName: 'Abydos Highschool',
  PlaceholderProductName: 'Anubis',
  RecentProjects: [],
  RecentProjectsLimit: 10,
}
const settingAliases = {
  language: 'Language',
  theme: 'Theme',
  themeStyle: 'ThemeStyle',
}
let backendState = null

function createBackendState() {
  return {
    initialized: false,
    settingsPath: null,
    appPath: null,
    currentProjectPath: null,
    currentProject: null,
    sceneGraphs: new Map(),
    uuidRegistry: [],
    settings: { ...defaultSettings },
  }
}

function getBackendState() {
  if (!backendState) backendState = createBackendState()
  return backendState
}

async function ensureBackendState(state, app) {
  if (state.initialized) return state
  state.appPath = app?.getAppPath ? app.getAppPath() : path.resolve(__dirname, '..', '..')
  const settingsRoot = app?.getPath ? app.getPath('userData') : path.join(os.homedir(), '.visunovia-editor')
  state.settingsPath = path.join(settingsRoot, 'editor-settings.json')
  try {
    const content = await fs.readFile(state.settingsPath, 'utf8')
    state.settings = normalizeSettings({ ...defaultSettings, ...JSON.parse(content) })
  } catch {
    state.settings = normalizeSettings(state.settings)
  }
  state.initialized = true
  return state
}

function normalizeSettings(settings) {
  const normalized = { ...defaultSettings, ...(settings || {}) }
  for (const [alias, canonical] of Object.entries(settingAliases)) {
    if (Object.prototype.hasOwnProperty.call(normalized, alias)) {
      normalized[canonical] = normalized[alias]
      delete normalized[alias]
    }
  }
  return normalized
}

async function saveEditorSettings(state) {
  if (!state.settingsPath) return
  await fs.mkdir(path.dirname(state.settingsPath), { recursive: true })
  await fs.writeFile(state.settingsPath, JSON.stringify(state.settings, null, 2), 'utf8')
}

function getRecentProjects(state) {
  const projects = Array.isArray(state.settings.RecentProjects) ? state.settings.RecentProjects : []
  return projects
    .filter(project => project && typeof project.path === 'string' && project.path.trim())
    .map(project => ({
      name: String(project.name || path.basename(path.dirname(project.path)) || project.path),
      path: path.resolve(project.path),
      lastOpened: project.lastOpened || null,
    }))
}

async function addRecentProject(state, projectPath, projectName) {
  const normalizedPath = path.resolve(projectPath)
  const limit = Math.max(1, Number.parseInt(state.settings.RecentProjectsLimit, 10) || defaultSettings.RecentProjectsLimit)
  const projects = getRecentProjects(state).filter(project => project.path.toLowerCase() !== normalizedPath.toLowerCase())
  projects.unshift({ name: projectName || path.basename(path.dirname(normalizedPath)), path: normalizedPath, lastOpened: new Date().toISOString() })
  state.settings.RecentProjects = projects.slice(0, limit)
  await saveEditorSettings(state)
}

function getCurrentLanguage(state) {
  return state.settings.Language || defaultSettings.Language
}

function getBundledAppRoot(state) {
  return state.appPath || path.resolve(__dirname, '..', '..')
}

function uuidRegistryPath(state) {
  if (!state.currentProject?.projectPath) return null
  return path.join(state.currentProject.projectPath, 'Settings', 'Editor', 'UuidRegistry.json')
}

async function loadUuidRegistry(state) {
  const registryPath = uuidRegistryPath(state)
  if (!registryPath || !(await exists(registryPath))) {
    state.uuidRegistry = []
    return state.uuidRegistry
  }
  state.uuidRegistry = JSON.parse(await fs.readFile(registryPath, 'utf8'))
  return state.uuidRegistry
}

async function saveUuidRegistry(state) {
  const registryPath = uuidRegistryPath(state)
  if (!registryPath) return
  await fs.mkdir(path.dirname(registryPath), { recursive: true })
  await fs.writeFile(registryPath, JSON.stringify(state.uuidRegistry, null, 2), 'utf8')
}

async function upsertUuidEntries(state, entries) {
  const now = new Date().toISOString()
  const byUuid = new Map((state.uuidRegistry || []).map(entry => [entry.uuid, entry]))
  for (const entry of entries) {
    if (!entry.uuid) continue
    const existing = byUuid.get(entry.uuid)
    byUuid.set(entry.uuid, {
      uuid: entry.uuid,
      entityType: entry.entityType || entry.entity_type || 'Unknown',
      name: entry.name || entry.uuid,
      displayName: entry.displayName || entry.display_name || entry.name || entry.uuid,
      createdAt: existing?.createdAt || entry.createdAt || now,
      updatedAt: now,
      detail: entry.detail || existing?.detail || null,
    })
  }
  state.uuidRegistry = Array.from(byUuid.values())
  await saveUuidRegistry(state)
}

function ok(data, status = 200) {
  return { status, data }
}

function apiSuccess(data, extra = {}) {
  return { success: true, data, ...extra }
}

function apiError(error, status = 500) {
  return ok({ success: false, error }, status)
}

function getParam(request, name) {
  return request?.params && typeof request.params === 'object' ? request.params[name] : undefined
}

function normalizeRoute(url) {
  const value = String(url || '')
  return value.startsWith('/') ? value : `/${value}`
}

function assertSafePath(inputPath) {
  if (!inputPath || typeof inputPath !== 'string') throw new Error('路径参数不能为空')
  const targetPath = decodeURIComponent(inputPath).trim()
  if (targetPath.includes('..') || targetPath.includes('|')) throw new Error('路径包含非法字符')
  return path.resolve(targetPath)
}

async function exists(targetPath) {
  try {
    await fs.access(targetPath)
    return true
  } catch {
    return false
  }
}

function normalizeSceneId(sceneId) {
  const normalized = String(sceneId || '').trim().replace(/\.lor$/i, '')
  if (!normalized) throw new Error('场景 ID 不能为空')
  if (normalized === '.' || normalized === '..' || /[<>:"/\\|?*\x00-\x1f]/.test(normalized)) {
    throw new Error('场景 ID 包含文件名非法字符')
  }
  return normalized
}

async function writeFileAtomic(filePath, content) {
  const directory = path.dirname(filePath)
  await fs.mkdir(directory, { recursive: true })
  const temporaryPath = path.join(directory, `.${path.basename(filePath)}.${process.pid}.${Date.now()}.tmp`)
  try {
    await fs.writeFile(temporaryPath, content, 'utf8')
    await fs.rename(temporaryPath, filePath)
  } catch (error) {
    await fs.rm(temporaryPath, { force: true }).catch(() => {})
    throw error
  }
}

function getContentType(filePath) {
  switch (path.extname(filePath).toLowerCase()) {
    case '.html': return 'text/html; charset=utf-8'
    case '.js': return 'text/javascript; charset=utf-8'
    case '.css': return 'text/css; charset=utf-8'
    case '.json': return 'application/json; charset=utf-8'
    case '.png': return 'image/png'
    case '.jpg':
    case '.jpeg': return 'image/jpeg'
    case '.gif': return 'image/gif'
    case '.webp': return 'image/webp'
    case '.bmp': return 'image/bmp'
    case '.svg': return 'image/svg+xml'
    case '.ico': return 'image/x-icon'
    case '.mp3': return 'audio/mpeg'
    case '.wav': return 'audio/wav'
    case '.ogg':
    case '.oga': return 'audio/ogg'
    case '.flac': return 'audio/flac'
    case '.m4a': return 'audio/mp4'
    case '.aac': return 'audio/aac'
    case '.opus': return 'audio/opus'
    case '.weba': return 'audio/webm'
    case '.mod':
    case '.xm':
    case '.s3m':
    case '.it':
    case '.mptm': return 'audio/x-mod'
    case '.mp4': return 'video/mp4'
    case '.webm': return 'video/webm'
    case '.m4v': return 'video/x-m4v'
    case '.mov': return 'video/quicktime'
    case '.ogv': return 'video/ogg'
    default: return 'application/octet-stream'
  }
}

function createBlankLorJson(sceneId) {
  return JSON.stringify({
    id: sceneId,
    background: '',
    bgm: { path: '', volume: 80, loop: true },
    dialogues: [],
  }, null, 2)
}

function createBlankSceneGraph(sceneId) {
  return {
    id: sceneId,
    viewport: { x: 0, y: 0, zoom: 1 },
    nodes: [],
    edges: [],
    sceneConfig: {},
  }
}

function createNode(id, type, x, y, data = {}) {
  return { id, type, position: { x, y }, data, inputs: {}, outputs: {} }
}

function lorToSceneGraph(sceneId, content) {
  let document
  try {
    document = JSON.parse(content || '{}')
  } catch {
    document = {}
  }

  if (document.nodes && Array.isArray(document.nodes)) {
    return {
      id: document.id || document.metadata?.scene_id || sceneId,
      viewport: document.viewport || { x: 0, y: 0, zoom: 1 },
      nodes: document.nodes,
      edges: document.edges || document.connections || [],
      connections: document.connections || document.edges || [],
      sceneConfig: document.sceneConfig || {},
    }
  }

  const nodes = [createNode('start', 'StartNode', 80, 160), createNode('end', 'EndNode', 760, 160)]
  const edges = []
  let previous = 'start'
  const dialogues = Array.isArray(document.dialogues) ? document.dialogues : []
  dialogues.forEach((dialogue, index) => {
    const id = dialogue.uuid || dialogue.id || `dialogue-${index + 1}`
    nodes.push(createNode(id, 'DialogueNode', 260 + index * 220, 160, {
      speaker: dialogue.speaker || '',
      text: dialogue.text || '',
      voice: dialogue.voice || '',
    }))
    edges.push({ id: `edge-${previous}-${id}`, from: { nodeId: previous, port: 'execOut' }, to: { nodeId: id, port: 'execIn' } })
    previous = id
  })
  edges.push({ id: `edge-${previous}-end`, from: { nodeId: previous, port: 'execOut' }, to: { nodeId: 'end', port: 'execIn' } })
  return { id: sceneId, viewport: { x: 0, y: 0, zoom: 1 }, nodes, edges, connections: edges, sceneConfig: { background: document.background, bgm: document.bgm } }
}

function sceneGraphToLor(sceneId, graph) {
  const nodes = Array.isArray(graph?.nodes) ? graph.nodes : []
  const connections = graph?.connections || graph?.edges || []
  const dialogues = nodes
    .filter(node => String(node.type || '').toLowerCase().includes('dialogue'))
    .map(node => ({
      uuid: node.id,
      speaker: node.data?.speaker || node.properties?.speaker || '',
      text: node.data?.text || node.properties?.text || '',
      voice: node.data?.voice || node.properties?.voice || '',
    }))

  return JSON.stringify({
    id: sceneId,
    background: graph?.sceneConfig?.background || '',
    bgm: graph?.sceneConfig?.bgm || { path: '', volume: 80, loop: true },
    dialogues,
    nodes,
    connections,
    edges: connections,
    viewport: graph?.viewport || { x: 0, y: 0, zoom: 1 },
  }, null, 2)
}

function isCustomEventNode(node) {
  return String(node?.type || node?.nodeType || '').toLowerCase() === 'customeventnode'
}

function getCustomEventScriptReference(nodeId) {
  const safeId = String(nodeId || '').replace(/[^a-zA-Z0-9_-]/g, '_')
  if (!safeId) throw new Error('Custom event node ID is required')
  return `Assets/CustomScripts/${safeId}.csx`
}

async function externalizeCustomEventScripts(graph, projectRoot) {
  const serialized = JSON.parse(JSON.stringify(graph || {}))
  const nodes = Array.isArray(serialized.nodes) ? serialized.nodes : []
  for (const node of nodes) {
    if (!isCustomEventNode(node)) continue
    const data = node.data && typeof node.data === 'object' ? node.data : (node.data = {})
    const code = typeof data.code === 'string' ? data.code : ''
    const scriptRef = String(data.scriptRef || getCustomEventScriptReference(node.id)).replace(/\\/g, '/')
    const scriptPath = path.resolve(projectRoot, scriptRef)
    const scriptsRoot = path.resolve(projectRoot, 'Assets', 'CustomScripts')
    if (!scriptPath.startsWith(scriptsRoot)) throw new Error('Custom event script path must be inside Assets/CustomScripts')
    if (code) {
      await writeFileAtomic(scriptPath, code)
    }
    data.scriptRef = scriptRef
    delete data.code
  }
  return serialized
}

async function hydrateCustomEventScripts(graph, projectRoot) {
  const hydrated = JSON.parse(JSON.stringify(graph || {}))
  const nodes = Array.isArray(hydrated.nodes) ? hydrated.nodes : []
  for (const node of nodes) {
    if (!isCustomEventNode(node)) continue
    const data = node.data && typeof node.data === 'object' ? node.data : (node.data = {})
    const scriptRef = String(data.scriptRef || getCustomEventScriptReference(node.id)).replace(/\\/g, '/')
    const scriptPath = path.resolve(projectRoot, scriptRef)
    const scriptsRoot = path.resolve(projectRoot, 'Assets', 'CustomScripts')
    if (!scriptPath.startsWith(scriptsRoot)) throw new Error('Custom event script path must be inside Assets/CustomScripts')
    data.scriptRef = scriptRef
    data.code = await exists(scriptPath) ? await fs.readFile(scriptPath, 'utf8') : ''
  }
  return hydrated
}

function exportBlueprintJson(sceneId, graph, metadata = {}) {
  const nodes = Array.isArray(graph?.nodes) ? graph.nodes : []
  const edges = graph?.edges || graph?.connections || []
  const registry = []
  nodes.forEach(node => registry.push({ uuid: node.id, entityType: 'Node', name: node.type || 'Node', displayName: node.data?.title || node.type || node.id, createdAt: new Date().toISOString(), updatedAt: new Date().toISOString() }))
  edges.forEach(edge => registry.push({ uuid: edge.id, entityType: 'Edge', name: edge.id, displayName: edge.id, createdAt: new Date().toISOString(), updatedAt: new Date().toISOString() }))
  return JSON.stringify({
    version: '1.0',
    metadata: { scene_id: sceneId, display_name: metadata.displayName || sceneId, description: metadata.description || '', author: metadata.author || '' },
    uuid_registry: registry,
    nodes: nodes.map(node => ({ uuid: node.id, type: node.type, position: node.position, data: node.data || node.properties || {} })),
    edges: edges.map(edge => ({ uuid: edge.id, source_node_uuid: edge.from?.nodeId || edge.source, source_port: edge.from?.port || edge.sourcePort, target_node_uuid: edge.to?.nodeId || edge.target, target_port: edge.to?.port || edge.targetPort })),
    resources: [],
    graph,
  }, null, 2)
}

function importBlueprintJson(sceneId, jsonContent) {
  const document = JSON.parse(jsonContent)
  if (document.graph) return { ...document.graph, id: sceneId }
  const nodes = (document.nodes || []).map(node => ({ id: node.uuid || node.id, type: node.type || 'UnknownNode', position: node.position || { x: 0, y: 0 }, data: node.data || {} }))
  const edges = (document.edges || []).map(edge => ({ id: edge.uuid || edge.id, from: { nodeId: edge.source_node_uuid || edge.source, port: edge.source_port || edge.sourcePort || 'execOut' }, to: { nodeId: edge.target_node_uuid || edge.target, port: edge.target_port || edge.targetPort || 'execIn' } }))
  return { id: sceneId, viewport: document.viewport || { x: 0, y: 0, zoom: 1 }, nodes, edges, connections: edges, sceneConfig: document.sceneConfig || {} }
}

function parsePoText(content) {
  const entries = []
  const blocks = String(content || '').split(/\r?\n\s*\r?\n/g)
  for (const block of blocks) {
    const lines = block.split(/\r?\n/g)
    let current = null
    const entry = { msgid: '', msgstr: '', comments: [] }
    for (const rawLine of lines) {
      const line = rawLine.trim()
      if (!line) continue
      if (line.startsWith('#')) { entry.comments.push(line); continue }
      const match = line.match(/^(msgid|msgstr)\s+"(.*)"$/)
      if (match) {
        current = match[1]
        entry[current] = unescapePo(match[2])
        continue
      }
      const continuation = line.match(/^"(.*)"$/)
      if (continuation && current) entry[current] += unescapePo(continuation[1])
    }
    if (entry.msgid || entry.msgstr) entries.push(entry)
  }
  return entries
}

function unescapePo(value) {
  return String(value || '').replace(/\\n/g, '\n').replace(/\\t/g, '\t').replace(/\\"/g, '"').replace(/\\\\/g, '\\')
}

function escapePo(value) {
  return String(value || '').replace(/\\/g, '\\\\').replace(/"/g, '\\"').replace(/\n/g, '\\n').replace(/\t/g, '\\t')
}

function writePoText(entries) {
  return entries.map(entry => `${(entry.comments || []).join('\n')}${entry.comments?.length ? '\n' : ''}msgid "${escapePo(entry.msgid)}"\nmsgstr "${escapePo(entry.msgstr)}"`).join('\n\n') + '\n'
}

function createProjectXml({ name, companyName, version, versionCode }) {
  return `<?xml version="1.0" encoding="utf-8"?>\n<project version="1.1">\n  <metadata>\n    <title>${escapeXml(name)}</title>\n    <author></author>\n    <version>${escapeXml(version || '1.0')}</version>\n    <versionCode>${escapeXml(versionCode || '1')}</versionCode>\n    <companyName>${escapeXml(companyName || '')}</companyName>\n    <ratingSystem>CADPA</ratingSystem>\n    <ratingValue>8+</ratingValue>\n  </metadata>\n  <scenes>\n    <scene id="start" />\n  </scenes>\n</project>\n`
}

function escapeXml(value) {
  return String(value || '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&apos;')
}

function extractXmlValue(xml, tag, fallback = '') {
  const match = xml.match(new RegExp(`<${tag}>([\\s\\S]*?)</${tag}>`, 'i'))
  return match ? decodeXml(match[1].trim()) : fallback
}

function decodeXml(value) {
  return String(value || '')
    .replace(/&apos;/g, "'")
    .replace(/&quot;/g, '"')
    .replace(/&gt;/g, '>')
    .replace(/&lt;/g, '<')
    .replace(/&amp;/g, '&')
}

async function ensureProjectDirs(projectRoot) {
  const dirs = [
    'UI', 'Scripts/Main', 'Locales/Engine', 'Locales', 'Assets/Characters',
    'Assets/Backgrounds', 'Assets/Musics', 'Assets/Voices', 'Assets/Sfx',
    'Assets/Emoji', 'Saves', 'Settings/Editor',
  ]
  await Promise.all(dirs.map(dir => fs.mkdir(path.join(projectRoot, dir), { recursive: true })))
}

async function buildFolderTree(targetPath) {
  const stat = await fs.stat(targetPath)
  const node = {
    name: path.basename(targetPath),
    path: targetPath,
    isDirectory: stat.isDirectory(),
    extension: stat.isDirectory() ? '' : path.extname(targetPath).toLowerCase(),
    size: stat.isDirectory() ? 0 : stat.size,
    lastModified: stat.mtime.toISOString(),
    children: null,
  }

  if (stat.isDirectory()) {
    const entries = await fs.readdir(targetPath)
    node.children = []
    for (const entry of entries.sort((a, b) => a.localeCompare(b))) {
      try {
        node.children.push(await buildFolderTree(path.join(targetPath, entry)))
      } catch {
        // Skip unreadable items.
      }
    }
  }

  return node
}

async function loadProject(state, projectPath, { trackRecent = true } = {}) {
  const resolvedPath = assertSafePath(projectPath)
  const stat = await fs.stat(resolvedPath)
  const projectRoot = stat.isDirectory() ? resolvedPath : path.dirname(resolvedPath)
  const tlorPath = stat.isDirectory() ? path.join(projectRoot, 'Project.tlor') : resolvedPath

  if (!(await exists(tlorPath))) throw new Error('未找到 Project.tlor 文件')
  const xml = await fs.readFile(tlorPath, 'utf8')
  const projectName = extractXmlValue(xml, 'title', path.basename(projectRoot))
  const version = extractXmlValue(xml, 'version', '1.0')
  const versionCode = extractXmlValue(xml, 'versionCode', '1')
  const companyName = extractXmlValue(xml, 'companyName', '')
  const ratingSystem = extractXmlValue(xml, 'ratingSystem', 'CADPA')
  const ratingValue = extractXmlValue(xml, 'ratingValue', '8+')

  const scenes = await readProjectScenes(projectRoot)
  state.currentProjectPath = tlorPath
  state.currentProject = { projectName, version, versionCode, companyName, ratingSystem, ratingValue, projectPath: projectRoot, scenes }
  state.settings.LastProjectPath = tlorPath
  if (trackRecent) await addRecentProject(state, tlorPath, projectName)
  else await saveEditorSettings(state)
  await loadUuidRegistry(state)
  for (const scene of scenes) {
    state.sceneGraphs.set(scene.id, lorToSceneGraph(scene.id, scene.content))
  }
  return state.currentProject
}

async function readProjectScenes(projectRoot) {
  const scriptsDir = path.join(projectRoot, 'Scripts', 'Main')
  if (!(await exists(scriptsDir))) return []
  const files = (await fs.readdir(scriptsDir)).filter(file => file.toLowerCase().endsWith('.lor')).sort((a, b) => a.localeCompare(b))
  const scenes = []
  for (const file of files) {
    const lorFilePath = path.join(scriptsDir, file)
    const id = path.basename(file, path.extname(file))
    const content = await fs.readFile(lorFilePath, 'utf8')
    scenes.push({ id, lorFilePath, content })
  }
  return scenes
}

async function newProject(request, state) {
  const name = String(request?.data?.name || '').trim()
  const basePath = assertSafePath(request?.data?.path)
  if (!name) return apiError('项目名称不能为空', 400)

  const projectRoot = path.join(basePath, name)
  if (await exists(projectRoot)) {
    const entries = await fs.readdir(projectRoot)
    if (entries.length > 0) return apiError('该目录下已存在同名项目文件夹且不为空', 409)
  }

  await ensureProjectDirs(projectRoot)
  const tlorPath = path.join(projectRoot, 'Project.tlor')
  await fs.writeFile(tlorPath, createProjectXml({
    name,
    companyName: request?.data?.companyName,
    version: request?.data?.version,
    versionCode: request?.data?.versionCode,
  }), 'utf8')

  const startPath = path.join(projectRoot, 'Scripts', 'Main', 'start.lor')
  await fs.writeFile(startPath, createBlankLorJson('start'), 'utf8')
  state.sceneGraphs.set('start', createBlankSceneGraph('start'))
  await loadProject(state, projectRoot)

  return ok(apiSuccess({
    projectPath: projectRoot,
    tlorPath,
    name,
    folderTree: await buildFolderTree(projectRoot),
  }))
}

async function importProject(request, state) {
  const project = await loadProject(state, request?.data?.projectPath)
  return ok(apiSuccess({ scenes: project.scenes }))
}

async function getCurrentProject(state) {
  if (!state.currentProject) return ok({ success: true, data: null, message: '当前没有打开的项目' })
  const { projectName, version, versionCode, companyName, ratingSystem, ratingValue, projectPath } = state.currentProject
  const subDirectories = (await fs.readdir(projectPath, { withFileTypes: true }))
    .filter(entry => entry.isDirectory())
    .map(entry => entry.name)
  return ok(apiSuccess({ projectName, version, versionCode, companyName, ratingSystem, ratingValue, projectPath, subDirectories }))
}

async function updateProjectSettings(request, state) {
  if (!state.currentProject) return apiError('当前没有打开的项目', 400)
  const projectName = String(request?.data?.projectName || '').trim()
  if (!projectName) return apiError('项目名称不能为空', 400)

  const tlorPath = state.currentProjectPath || path.join(state.currentProject.projectPath, 'Project.tlor')
  const xml = await fs.readFile(tlorPath, 'utf8')
  const titlePattern = /<title>[\s\S]*?<\/title>/i
  if (!titlePattern.test(xml)) return apiError('Project.tlor 缺少 title 元素', 400)
  await writeFileAtomic(tlorPath, xml.replace(titlePattern, `<title>${escapeXml(projectName)}</title>`))
  state.currentProject.projectName = projectName
  return ok(apiSuccess({ projectName }))
}

async function getProjectScenes(request, state) {
  const projectRoot = getParam(request, 'projectPath') || state.currentProject?.projectPath
  if (!projectRoot) return apiError('项目路径不能为空', 400)
  const scenes = await readProjectScenes(assertSafePath(projectRoot))
  return ok(apiSuccess(scenes.map(scene => ({ id: scene.id, lorFilePath: scene.lorFilePath }))))
}

async function getSceneContent(request) {
  const filePath = assertSafePath(getParam(request, 'path'))
  const content = await fs.readFile(filePath, 'utf8')
  return ok(apiSuccess({ id: path.basename(filePath, path.extname(filePath)), lorFilePath: filePath, content }))
}

async function getProjectFileContent(request) {
  const filePath = assertSafePath(getParam(request, 'path'))
  const stat = await fs.stat(filePath)
  if (stat.size > 1024 * 1024) return apiError('文件过大，无法预览', 400)
  return ok(apiSuccess({ path: filePath, name: path.basename(filePath), content: await fs.readFile(filePath, 'utf8') }))
}

async function createScene(request, state) {
  if (!state.currentProject) return apiError('当前没有打开的项目', 400)
  const sceneId = normalizeSceneId(request?.data?.sceneId)
  const filePath = path.join(state.currentProject.projectPath, 'Scripts', 'Main', `${sceneId}.lor`)
  if (await exists(filePath)) return apiError('场景已存在', 409)
  await writeFileAtomic(filePath, createBlankLorJson(sceneId))
  state.sceneGraphs.set(sceneId, createBlankSceneGraph(sceneId))
  state.currentProject.scenes = await readProjectScenes(state.currentProject.projectPath)
  return ok({ success: true })
}

async function deleteScene(route, state) {
  if (!state.currentProject) return apiError('当前没有打开的项目', 400)
  const sceneId = decodeURIComponent(route.split('/')[3] || '')
  if (!sceneId) return apiError('场景 ID 不能为空', 400)
  await fs.rm(path.join(state.currentProject.projectPath, 'Scripts', 'Main', `${sceneId}.lor`), { force: true })
  state.sceneGraphs.delete(sceneId)
  state.currentProject.scenes = await readProjectScenes(state.currentProject.projectPath)
  return ok({ success: true })
}

async function renameScene(route, request, state) {
  if (!state.currentProject) return apiError('当前没有打开的项目', 400)
  const sceneId = decodeURIComponent(route.split('/')[3] || '')
  const newSceneId = String(request?.data?.newSceneId || '').trim()
  if (!sceneId || !newSceneId) return apiError('场景 ID 不能为空', 400)
  const scriptsDir = path.join(state.currentProject.projectPath, 'Scripts', 'Main')
  await fs.rename(path.join(scriptsDir, `${sceneId}.lor`), path.join(scriptsDir, `${newSceneId}.lor`))
  if (state.sceneGraphs.has(sceneId)) {
    const graph = state.sceneGraphs.get(sceneId)
    graph.id = newSceneId
    state.sceneGraphs.delete(sceneId)
    state.sceneGraphs.set(newSceneId, graph)
  }
  state.currentProject.scenes = await readProjectScenes(state.currentProject.projectPath)
  return ok({ success: true })
}

async function getSceneGraph(sceneId, state) {
  if (state.sceneGraphs.has(sceneId)) {
    const graph = state.sceneGraphs.get(sceneId)
    const hydrated = state.currentProject
      ? await hydrateCustomEventScripts(graph, state.currentProject.projectPath)
      : graph
    state.sceneGraphs.set(sceneId, hydrated)
    return ok(apiSuccess(hydrated))
  }
  if (!state.currentProject) return ok(apiSuccess(createBlankSceneGraph(sceneId)))

  const graphPath = path.join(state.currentProject.projectPath, 'Settings', 'Editor', `${sceneId}.scenegraph.json`)
  if (await exists(graphPath)) {
    const graph = JSON.parse(await fs.readFile(graphPath, 'utf8'))
    const hydrated = await hydrateCustomEventScripts(graph, state.currentProject.projectPath)
    state.sceneGraphs.set(sceneId, hydrated)
    return ok(apiSuccess(hydrated))
  }
  return ok(apiSuccess(createBlankSceneGraph(sceneId)))
}

async function saveSceneGraph(sceneId, graph, state) {
  if (!state.currentProject) return apiError('请先打开或创建项目后再保存场景', 409)
  const normalizedSceneId = normalizeSceneId(sceneId)
  const projectRoot = state.currentProject.projectPath
  const sceneGraph = await externalizeCustomEventScripts({ ...graph, id: normalizedSceneId }, projectRoot)
  const hydratedSceneGraph = await hydrateCustomEventScripts(sceneGraph, projectRoot)
  const graphPath = path.join(projectRoot, 'Settings', 'Editor', `${normalizedSceneId}.scenegraph.json`)
  const lorPath = path.join(projectRoot, 'Scripts', 'Main', `${normalizedSceneId}.lor`)
  await writeFileAtomic(graphPath, JSON.stringify(sceneGraph, null, 2))
  await writeFileAtomic(lorPath, sceneGraphToLor(normalizedSceneId, sceneGraph))
  state.sceneGraphs.set(normalizedSceneId, hydratedSceneGraph)
  state.currentProject.scenes = await readProjectScenes(projectRoot)
  const nodes = Array.isArray(sceneGraph.nodes) ? sceneGraph.nodes : []
  const edges = sceneGraph.edges || sceneGraph.connections || []
  await upsertUuidEntries(state, [
    { uuid: normalizedSceneId, entityType: 'Scene', name: normalizedSceneId, displayName: normalizedSceneId, detail: { sceneId: normalizedSceneId } },
    ...nodes.map(node => ({ uuid: node.id, entityType: 'Node', name: node.type || node.id, displayName: node.data?.title || node.type || node.id, detail: node })),
    ...edges.map(edge => ({ uuid: edge.id, entityType: 'Edge', name: edge.id, displayName: edge.id, detail: edge })),
  ])
  return ok(apiSuccess(hydratedSceneGraph, { message: '场景图已保存' }))
}

async function importProjectAsset(request, state) {
  if (!state.currentProject) return apiError('当前没有打开的项目', 400)
  const targetDirectory = String(request?.data?.fields?.targetDirectory || '').trim()
  const uploads = [request?.data?.files?.file].flat().filter(Boolean)
  if (!targetDirectory || uploads.length === 0) return apiError('请选择要导入的资产文件', 400)
  const assetsRoot = path.join(state.currentProject.projectPath, 'Assets')
  const targetPath = path.resolve(targetDirectory)
  if (!targetPath.startsWith(path.resolve(assetsRoot))) return apiError('目标目录必须位于 Assets 下', 400)
  const imported = []
  for (const upload of uploads) {
    const uploadPath = String(request?.data?.fields?.relativePath || upload.filename || '').replace(/\\/g, '/')
    const fileName = path.basename(uploadPath)
    if (!fileName || fileName.includes('..')) return apiError('文件名无效', 400)
    const relativeDirectory = path.dirname(uploadPath) === '.' ? '' : path.dirname(uploadPath)
    const destinationDirectory = path.resolve(targetPath, relativeDirectory)
    if (!destinationDirectory.startsWith(targetPath)) return apiError('目录路径无效', 400)
    const destinationPath = path.join(destinationDirectory, fileName)
    if (await exists(destinationPath)) return apiError(`目标目录已存在同名资产: ${fileName}`, 409)
    await fs.mkdir(destinationDirectory, { recursive: true })
    await fs.writeFile(destinationPath, upload.buffer)
    imported.push(await buildFolderTree(destinationPath))
  }
  return ok(apiSuccess(imported.length === 1 ? imported[0] : imported))
}

async function deleteProjectAsset(request, state) {
  if (!state.currentProject) return apiError('当前没有打开的项目', 400)
  const targetPath = assertSafePath(getParam(request, 'path'))
  const assetsRoot = path.resolve(state.currentProject.projectPath, 'Assets')
  if (!targetPath.startsWith(assetsRoot)) return apiError('资产路径必须位于 Assets 下', 400)
  await fs.rm(targetPath, { recursive: true, force: true })
  return ok({ success: true })
}

async function renameProjectAsset(request, state) {
  if (!state.currentProject) return apiError('当前没有打开的项目', 400)
  const targetPath = assertSafePath(request?.data?.path)
  const newName = String(request?.data?.newName || '').trim()
  if (!newName || /[\\/:*?"<>|]/.test(newName)) return apiError('新名称无效', 400)
  const assetsRoot = path.resolve(state.currentProject.projectPath, 'Assets')
  if (!targetPath.startsWith(assetsRoot)) return apiError('资产路径必须位于 Assets 下', 400)
  const destinationPath = path.join(path.dirname(targetPath), newName)
  await fs.rename(targetPath, destinationPath)
  return ok(apiSuccess(await buildFolderTree(destinationPath)))
}

async function exportJson(route, request, state) {
  const sceneId = decodeURIComponent(route.split('/')[3] || '')
  if (!sceneId) return apiError('场景 ID 不能为空', 400)
  const graph = state.sceneGraphs.get(sceneId) || createBlankSceneGraph(sceneId)
  const nodes = Array.isArray(graph.nodes) ? graph.nodes : []
  const edges = graph.edges || graph.connections || []
  await upsertUuidEntries(state, [
    { uuid: sceneId, entityType: 'Scene', name: sceneId, displayName: sceneId, detail: { sceneId } },
    ...nodes.map(node => ({ uuid: node.id, entityType: 'Node', name: node.type || node.id, displayName: node.data?.title || node.type || node.id, detail: node })),
    ...edges.map(edge => ({ uuid: edge.id, entityType: 'Edge', name: edge.id, displayName: edge.id, detail: edge })),
  ])
  return ok(apiSuccess({
    sceneId,
    jsonContent: exportBlueprintJson(sceneId, graph, request.params || {}),
    exportedAt: new Date().toISOString(),
  }))
}

async function importJson(route, request, state) {
  const sceneId = decodeURIComponent(route.split('/')[3] || '')
  if (!sceneId) return apiError('场景 ID 不能为空', 400)
  const graph = importBlueprintJson(sceneId, String(request?.data?.jsonContent || ''))
  await saveSceneGraph(sceneId, graph, state)
  const document = JSON.parse(String(request?.data?.jsonContent || '{}'))
  await upsertUuidEntries(state, (document.uuid_registry || document.uuidRegistry || []).map(entry => ({
    uuid: entry.uuid,
    entityType: entry.entityType || entry.entity_type,
    name: entry.name,
    displayName: entry.displayName || entry.display_name,
    detail: entry,
  })))
  return ok(apiSuccess({ sceneId, nodeCount: graph.nodes?.length || 0, edgeCount: (graph.edges || graph.connections || []).length, importedAt: new Date().toISOString() }))
}

async function uploadJson(route, request, state) {
  const sceneId = decodeURIComponent(route.split('/')[3] || '')
  const upload = request?.data?.files?.file
  if (!upload?.buffer) return apiError('未选择文件', 400)
  const graph = importBlueprintJson(sceneId, upload.buffer.toString('utf8'))
  await saveSceneGraph(sceneId, graph, state)
  return ok(apiSuccess({ sceneId, fileName: upload.filename, nodeCount: graph.nodes?.length || 0, edgeCount: (graph.edges || graph.connections || []).length, importedAt: new Date().toISOString() }))
}

function validateJson(request) {
  try {
    const document = JSON.parse(String(request?.data?.jsonContent || ''))
    const nodes = Array.isArray(document.nodes) ? document.nodes : []
    const edges = Array.isArray(document.edges) ? document.edges : []
    const registry = Array.isArray(document.uuid_registry) ? document.uuid_registry : []
    const errors = []
    const warnings = []
    if (!document.version) warnings.push('缺少版本信息')
    if (!document.metadata?.scene_id && !document.id) errors.push('缺少场景 ID')
    const nodeIds = new Set(nodes.map(node => node.uuid || node.id))
    for (const edge of edges) {
      const source = edge.source_node_uuid || edge.source
      const target = edge.target_node_uuid || edge.target
      if (source && !nodeIds.has(source)) errors.push(`连线 ${edge.uuid || edge.id || ''} 引用了不存在的源节点: ${source}`)
      if (target && !nodeIds.has(target)) errors.push(`连线 ${edge.uuid || edge.id || ''} 引用了不存在的目标节点: ${target}`)
    }
    return ok({ success: true, valid: errors.length === 0, errors, warnings, stats: { nodeCount: nodes.length, edgeCount: edges.length, resourceCount: document.resources?.length || 0, uuidCount: registry.length } })
  } catch (error) {
    return ok({ success: false, valid: false, errors: [`Lor JSON 格式错误: ${error instanceof Error ? error.message : String(error)}`] })
  }
}

async function getPoTranslations(state, language) {
  const candidates = [
    path.join(getBundledAppRoot(state), 'Localizations', `${language}.po`),
    path.join(path.resolve(__dirname, '..', '..'), 'Localizations', `${language}.po`),
    path.join(process.cwd(), 'Localizations', `${language}.po`),
  ]
  if (state.currentProject) candidates.unshift(path.join(state.currentProject.projectPath, 'Locales', `${language}.po`))
  const poPath = candidates.find(candidate => require('node:fs').existsSync(candidate))
  if (!poPath) return { language, translations: {} }
  const entries = parsePoText(await fs.readFile(poPath, 'utf8'))
  return { language, translations: Object.fromEntries(entries.filter(entry => entry.msgid && entry.msgstr).map(entry => [entry.msgid.toLowerCase(), entry.msgstr])) }
}

async function getAvailableLanguages(state) {
  const roots = [
    path.join(getBundledAppRoot(state), 'Localizations'),
    path.join(path.resolve(__dirname, '..', '..'), 'Localizations'),
    path.join(process.cwd(), 'Localizations'),
  ]
  if (state.currentProject) roots.unshift(path.join(state.currentProject.projectPath, 'Locales'))
  const languageCodes = new Set()
  for (const root of roots) {
    try {
      const files = await fs.readdir(root)
      files.filter(file => file.toLowerCase().endsWith('.po')).forEach(file => languageCodes.add(path.basename(file, '.po')))
    } catch {}
  }

  const currentLanguage = getCurrentLanguage(state)
  const languages = []
  for (const code of Array.from(languageCodes).sort((left, right) => left.localeCompare(right))) {
    const { translations } = await getPoTranslations(state, code)
    const displayName = translations['common.languagedisplayname'] || code
    languages.push({
      code,
      displayName,
      nativeName: displayName,
      isCurrent: code.toLowerCase() === currentLanguage.toLowerCase(),
    })
  }
  return languages
}

async function savePoTranslations(state, language, translations) {
  const targetRoot = state.currentProject ? path.join(state.currentProject.projectPath, 'Locales') : path.join(process.cwd(), 'Localizations')
  const poPath = path.join(targetRoot, `${language}.po`)
  await fs.mkdir(targetRoot, { recursive: true })
  let entries = []
  if (await exists(poPath)) entries = parsePoText(await fs.readFile(poPath, 'utf8'))
  const byId = new Map(entries.map(entry => [entry.msgid, entry]))
  for (const [msgid, msgstr] of Object.entries(translations || {})) {
    const existing = byId.get(msgid) || { msgid, msgstr: '', comments: [] }
    existing.msgstr = String(msgstr ?? '')
    byId.set(msgid, existing)
  }
  await fs.writeFile(poPath, writePoText(Array.from(byId.values())), 'utf8')
  return { language, path: poPath, count: byId.size }
}

async function getDrives() {
  if (process.platform !== 'win32') return [{ letter: '/', name: 'Root', totalSpace: 0, freeSpace: 0, fileSystem: '' }]
  try {
    const command = '[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new(); Get-CimInstance Win32_LogicalDisk | Where-Object { $_.Size -ne $null } | Select-Object DeviceID,VolumeName,FileSystem,Size,FreeSpace | ConvertTo-Json -Compress'
    const { stdout } = await execFileAsync('powershell.exe', ['-NoProfile', '-NonInteractive', '-Command', command], { windowsHide: true })
    const parsed = JSON.parse(stdout.trim() || '[]')
    return (Array.isArray(parsed) ? parsed : [parsed]).map(drive => ({
      letter: String(drive.DeviceID || ''),
      name: String(drive.VolumeName || drive.DeviceID || ''),
      totalSpace: Number(drive.Size || 0),
      freeSpace: Number(drive.FreeSpace || 0),
      fileSystem: String(drive.FileSystem || ''),
    })).filter(drive => drive.letter)
  } catch {}

  const drives = []
  for (let code = 65; code <= 90; code++) {
    const letter = `${String.fromCharCode(code)}:`
    try {
      const root = `${letter}\\`
      await fs.access(root)
      const stats = await fs.statfs(root, { bigint: true })
      drives.push({
        letter,
        name: letter,
        totalSpace: Number(stats.bsize * stats.blocks),
        freeSpace: Number(stats.bsize * stats.bavail),
        fileSystem: '',
      })
    } catch {}
  }
  return drives
}

async function getEntries(request) {
  const targetPath = assertSafePath(getParam(request, 'path'))
  const stat = await fs.stat(targetPath)
  if (!stat.isDirectory()) return apiError(`路径不是目录: ${targetPath}`, 400)
  const names = await fs.readdir(targetPath)
  const entries = []
  for (const name of names) {
    const entryPath = path.join(targetPath, name)
    try {
      const entryStat = await fs.stat(entryPath)
      entries.push({ name, path: entryPath, isDirectory: entryStat.isDirectory(), size: entryStat.isDirectory() ? 0 : entryStat.size, lastModified: entryStat.mtime.toISOString(), extension: entryStat.isDirectory() ? '' : path.extname(name).toLowerCase() })
    } catch {}
  }
  entries.sort((left, right) => left.isDirectory !== right.isDirectory ? (left.isDirectory ? -1 : 1) : left.name.localeCompare(right.name))
  return ok(apiSuccess({ currentPath: targetPath, parentPath: path.dirname(targetPath), entries }))
}

async function createFolder(request) {
  const parentPath = assertSafePath(request?.data?.parentPath)
  const folderName = String(request?.data?.folderName || '').trim()
  if (!folderName) return apiError('文件夹名称不能为空', 400)
  if (/[\\/:*?"<>|]/.test(folderName) || folderName.includes('..')) return apiError('文件夹名称包含非法字符', 400)
  const targetPath = path.join(parentPath, folderName)
  await fs.mkdir(targetPath)
  return ok(apiSuccess({ path: targetPath }))
}

function getSpecialFolders() {
  const home = os.homedir()
  return [
    { id: 'desktop', name: '桌面', path: path.join(home, 'Desktop') },
    { id: 'documents', name: '文档', path: path.join(home, 'Documents') },
    { id: 'pictures', name: '图片', path: path.join(home, 'Pictures') },
    { id: 'userProfile', name: '用户主目录', path: home },
  ]
}

async function readText(request) {
  const targetPath = assertSafePath(getParam(request, 'path'))
  if (!(await exists(targetPath))) return apiError('文件不存在', 404)
  const extension = path.extname(targetPath).toLowerCase()
  if (!textExtensions.has(extension)) return apiError('不支持的文本文件格式', 400)
  const stat = await fs.stat(targetPath)
  if (stat.size > 1024 * 1024) return apiError('文件过大，无法读取', 400)
  const content = await fs.readFile(targetPath, 'utf8')
  return ok(apiSuccess({ path: targetPath, name: path.basename(targetPath), content }))
}

function scanResources(projectRoot) {
  const result = { sprites: [], backgrounds: [], bgm: [], voice: [], sfx: [] }
  const fsSync = require('node:fs')
  if (!projectRoot || !fsSync.existsSync(projectRoot)) return result
  for (const [category, parts] of Object.entries(assetCategories)) {
    const root = path.join(projectRoot, ...parts)
    if (!fsSync.existsSync(root)) continue
    const stack = [root]
    while (stack.length > 0) {
      const current = stack.pop()
      for (const entry of fsSync.readdirSync(current, { withFileTypes: true })) {
        const fullPath = path.join(current, entry.name)
        if (entry.isDirectory()) stack.push(fullPath)
        else if (mediaExtensions.has(path.extname(entry.name).toLowerCase())) result[category].push(path.relative(projectRoot, fullPath).replace(/\\/g, '/'))
      }
    }
  }
  return result
}

function resolveProjectResourcePath(state, resourcePath) {
  if (!state.currentProject?.projectPath) return null
  const projectRoot = path.resolve(state.currentProject.projectPath)
  const fullPath = path.resolve(projectRoot, resourcePath.replace(/[\\/]+/g, path.sep))
  if (!fullPath.startsWith(projectRoot)) return null
  return fullPath
}

async function getConfigFile(state, folder, fallback) {
  if (!state.currentProject) return apiError('当前没有打开的项目', 400)
  const configPath = path.join(state.currentProject.projectPath, 'Assets', folder, 'Manifest.resona')
  if (!(await exists(configPath))) return ok(apiSuccess(fallback))
  return ok(apiSuccess(JSON.parse(await fs.readFile(configPath, 'utf8'))))
}

async function saveConfigFile(state, folder, data) {
  if (!state.currentProject) return apiError('当前没有打开的项目', 400)
  const configRoot = path.join(state.currentProject.projectPath, 'Assets', folder)
  await fs.mkdir(configRoot, { recursive: true })
  await fs.writeFile(path.join(configRoot, 'Manifest.resona'), JSON.stringify(data, null, 2), 'utf8')
  return ok(apiSuccess(data))
}

async function handleBackendRequest(request, app, state = getBackendState()) {
  await ensureBackendState(state, app)
  const method = String(request?.method || 'GET').toUpperCase()
  const route = normalizeRoute(request?.url)

  try {
    if (method === 'GET' && route === '/system/health') return ok({ success: true, status: 'ok' })
    if (method === 'POST' && route === '/system/quit') { app.quit(); return ok({ success: true }) }

    if (method === 'POST' && route === '/project/new') return await newProject(request, state)
    if (method === 'POST' && route === '/project/import') return await importProject(request, state)
    if (method === 'GET' && route === '/project/currentProject') return await getCurrentProject(state)
    if (method === 'GET' && route === '/project/recentProjects') return ok(apiSuccess(getRecentProjects(state)))
    if (method === 'GET' && route === '/project/scenes') return await getProjectScenes(request, state)
    if (method === 'POST' && route === '/project/scenes') return await createScene(request, state)
    if (method === 'PUT' && route.startsWith('/project/scenes/') && route.endsWith('/rename')) return await renameScene(route, request, state)
    if (method === 'DELETE' && route.startsWith('/project/scenes/')) return await deleteScene(route, state)
    if (method === 'GET' && route === '/project/scene') return await getSceneContent(request)
    if (method === 'GET' && route === '/project/folder-tree') return ok(apiSuccess(await buildFolderTree(assertSafePath(getParam(request, 'projectPath')))))
    if (method === 'GET' && route === '/project/file-content') return await getProjectFileContent(request)
    if (method === 'POST' && route === '/project/assets/import') return await importProjectAsset(request, state)
    if (method === 'DELETE' && route === '/project/assets') return await deleteProjectAsset(request, state)
    if (method === 'PUT' && route === '/project/assets/rename') return await renameProjectAsset(request, state)
    if (method === 'PUT' && route === '/project/settings') return await updateProjectSettings(request, state)
    if (method === 'GET' && route === '/project/characters/config') return await getConfigFile(state, 'Characters', { characters: [] })
    if (method === 'PUT' && route === '/project/characters/config') return await saveConfigFile(state, 'Characters', request.data || { characters: [] })
    if (method === 'GET' && route === '/project/expressions/config') return await getConfigFile(state, 'Emoji', { expressions: [] })
    if (method === 'PUT' && route === '/project/expressions/config') return await saveConfigFile(state, 'Emoji', request.data || { expressions: [] })

    if (method === 'GET' && route === '/scenegraphs/list') return ok(apiSuccess(Array.from(state.sceneGraphs.keys())))
    if (method === 'GET' && route.startsWith('/scenegraphs/')) return await getSceneGraph(decodeURIComponent(route.split('/')[2]), state)
    if (method === 'PUT' && route.startsWith('/scenegraphs/')) return await saveSceneGraph(decodeURIComponent(route.split('/')[2]), request.data, state)

    if (method === 'GET' && route === '/resources') return ok(scanResources(state.currentProject?.projectPath || ''))
    if (method === 'GET' && route.startsWith('/resources/')) return ok([])

    if (method === 'GET' && route.startsWith('/json/export/')) return await exportJson(route, request, state)
    if (method === 'POST' && route.startsWith('/json/import/')) return await importJson(route, request, state)
    if (method === 'POST' && route.startsWith('/json/upload/')) return await uploadJson(route, request, state)
    if (method === 'POST' && route === '/json/validate') return validateJson(request)
    if (method === 'GET' && route === '/json/uuid-registry') return ok(apiSuccess(state.uuidRegistry || []))
    if (method === 'GET' && route.startsWith('/json/uuid-registry/detail/')) {
      const uuid = decodeURIComponent(route.split('/')[4] || '')
      const entry = (state.uuidRegistry || []).find(item => item.uuid === uuid)
      return entry ? ok(apiSuccess(entry)) : apiError(`UUID ${uuid} 不存在`, 404)
    }
    if (method === 'GET' && route.startsWith('/json/uuid-registry/')) {
      const entityType = decodeURIComponent(route.split('/')[3] || '')
      return ok(apiSuccess((state.uuidRegistry || []).filter(item => String(item.entityType).toLowerCase() === entityType.toLowerCase())))
    }
    if (method === 'GET' && route.startsWith('/json/snapshot/')) {
      const sceneId = decodeURIComponent(route.split('/')[3] || '')
      const graph = state.sceneGraphs.get(sceneId)
      if (!graph) return apiError(`场景 ${sceneId} 没有 JSON 快照`, 404)
      return ok(apiSuccess({ sceneId, jsonContent: exportBlueprintJson(sceneId, graph) }))
    }

    if (method === 'GET' && route === '/FileBrowser/drives') return ok(apiSuccess(await getDrives()))
    if (method === 'GET' && route === '/FileBrowser/entries') return await getEntries(request)
    if (method === 'POST' && route === '/FileBrowser/create-folder') return await createFolder(request)
    if (method === 'GET' && route === '/FileBrowser/special-folders') return ok(apiSuccess(getSpecialFolders()))
    if (method === 'GET' && route === '/FileBrowser/read-text') return await readText(request)

    if (method === 'GET' && route === '/settings') return ok(apiSuccess({ settings: state.settings, isRemoteSession: false }))
    if (method === 'PUT' && route === '/settings') {
      const settings = normalizeSettings(request?.data?.settings || request?.data || {})
      Object.assign(state.settings, settings)
      await saveEditorSettings(state)
      return ok(apiSuccess({ updatedCount: Object.keys(settings).length, invalidKeys: [] }))
    }

    if (method === 'GET' && route === '/localization/languages') return ok(apiSuccess(await getAvailableLanguages(state)))
    if (method === 'POST' && route === '/localization/language') {
      state.settings.Language = request?.data?.language || defaultSettings.Language
      await saveEditorSettings(state)
      return ok(apiSuccess({ success: true, newLanguage: state.settings.Language }))
    }
    if (method === 'GET' && route === '/localization/translations') return ok(apiSuccess(await getPoTranslations(state, getParam(request, 'lang') || getCurrentLanguage(state))))
    if ((method === 'PUT' || method === 'POST') && route === '/localization/translations') {
      const language = request?.data?.language || getParam(request, 'lang') || getCurrentLanguage(state)
      return ok(apiSuccess(await savePoTranslations(state, language, request?.data?.translations || {})))
    }
    if (method === 'GET' && (route === '/currentLang' || route === '/localization/currentLang')) {
      const msgId = String(getParam(request, 'msgId') || '')
      const translations = await getPoTranslations(state, getCurrentLanguage(state))
      return ok(translations.translations[msgId.toLowerCase()] || msgId)
    }

    return apiError(`Electron backend handler is not implemented for ${method} ${route}`, 501)
  } catch (error) {
    return apiError(error instanceof Error ? error.message : String(error), 500)
  }
}

module.exports = { handleBackendRequest, createBackendState, resolveProjectResourcePath, getContentType }
