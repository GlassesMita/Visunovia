const assert = require('node:assert/strict')
const fs = require('node:fs/promises')
const os = require('node:os')
const path = require('node:path')
const test = require('node:test')

const { createBackendState, handleBackendRequest } = require('../apps/editor-desktop/backend.cjs')

test('persists recently opened projects', async t => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), 'visunovia-backend-'))
  t.after(() => fs.rm(root, { recursive: true, force: true }))
  const userData = path.join(root, 'user-data')
  const app = {
    getAppPath: () => path.resolve(__dirname, '..'),
    getPath: () => userData,
  }

  const createResponse = await handleBackendRequest({
    method: 'POST',
    url: '/project/new',
    data: { name: 'Recent Test', path: root },
  }, app, createBackendState())
  assert.equal(createResponse.data.success, true)

  const restoredState = createBackendState()
  const currentResponse = await handleBackendRequest({
    method: 'GET',
    url: '/project/currentProject',
  }, app, restoredState)
  assert.equal(currentResponse.data.data, null)

  const recentResponse = await handleBackendRequest({
    method: 'GET',
    url: '/project/recentProjects',
  }, app, restoredState)
  assert.equal(recentResponse.data.success, true)
  assert.equal(recentResponse.data.data.length, 1)
  assert.equal(recentResponse.data.data[0].name, 'Recent Test')
  assert.equal(recentResponse.data.data[0].path, path.join(root, 'Recent Test', 'Project.tlor'))
  assert.ok(Date.parse(recentResponse.data.data[0].lastOpened))
})

test('reports non-zero space for mounted Windows drives', { skip: process.platform !== 'win32' }, async () => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), 'visunovia-drives-'))
  try {
    const app = {
      getAppPath: () => path.resolve(__dirname, '..'),
      getPath: () => root,
    }
    const response = await handleBackendRequest({ method: 'GET', url: '/FileBrowser/drives' }, app, createBackendState())
    const systemDrive = `${path.parse(process.cwd()).root.slice(0, 2)}`.toUpperCase()
    const drive = response.data.data.find(item => item.letter.toUpperCase() === systemDrive)
    assert.ok(drive)
    assert.ok(drive.totalSpace > 0)
    assert.ok(drive.freeSpace > 0)
    assert.ok(drive.totalSpace >= drive.freeSpace)
    assert.ok(drive.fileSystem)
    assert.equal(drive.name.includes('\uFFFD'), false)
  } finally {
    await fs.rm(root, { recursive: true, force: true })
  }
})

test('preserves scene connections after save and reopen', async t => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), 'visunovia-connections-'))
  t.after(() => fs.rm(root, { recursive: true, force: true }))
  const app = {
    getAppPath: () => path.resolve(__dirname, '..'),
    getPath: () => path.join(root, 'user-data'),
  }
  const state = createBackendState()
  await handleBackendRequest({
    method: 'POST',
    url: '/project/new',
    data: { name: 'Connection Test', path: root },
  }, app, state)

  const connection = {
    uuid: 'start:execOut->end:execIn',
    id: 'start:execOut->end:execIn',
    sourceNodeUuid: 'start',
    source: 'start',
    sourcePort: 'execOut',
    targetNodeUuid: 'end',
    target: 'end',
    targetPort: 'execIn',
  }
  const graph = {
    id: 'start',
    nodes: [
      { uuid: 'start', id: 'start', nodeType: 'StartNode', position: { x: 0, y: 0 }, properties: {}, nextNodeUuids: [] },
      { uuid: 'end', id: 'end', nodeType: 'EndNode', position: { x: 300, y: 0 }, properties: {}, nextNodeUuids: [] },
    ],
    connections: [connection],
  }
  const saveResponse = await handleBackendRequest({ method: 'PUT', url: '/scenegraphs/start', data: graph }, app, state)
  assert.equal(saveResponse.data.success, true)

  const lorPath = path.join(root, 'Connection Test', 'Scripts', 'Main', 'start.lor')
  const savedLor = JSON.parse(await fs.readFile(lorPath, 'utf8'))
  assert.deepEqual(savedLor.connections, [connection])

  const reopenedState = createBackendState()
  await handleBackendRequest({
    method: 'POST',
    url: '/project/import',
    data: { projectPath: path.join(root, 'Connection Test') },
  }, app, reopenedState)
  const reopenedGraph = await handleBackendRequest({ method: 'GET', url: '/scenegraphs/start' }, app, reopenedState)
  assert.deepEqual(reopenedGraph.data.data.connections, [connection])
})

test('stores custom event source in a .csx file instead of scene JSON', async t => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), 'visunovia-custom-event-'))
  t.after(() => fs.rm(root, { recursive: true, force: true }))
  const app = {
    getAppPath: () => path.resolve(__dirname, '..'),
    getPath: () => path.join(root, 'user-data'),
  }
  const state = createBackendState()
  await handleBackendRequest({
    method: 'POST',
    url: '/project/new',
    data: { name: 'Custom Event Test', path: root },
  }, app, state)

  const code = 'using System;\n\nEvent.CreateWindow(1, 100, 100, 1280, 720);\nExtension.Broadcast("ready");'
  const graph = {
    id: 'start',
    nodes: [{
      id: 'custom-event-1',
      type: 'CustomEventNode',
      position: { x: 0, y: 0 },
      data: { code },
    }],
    connections: [],
  }
  const saveResponse = await handleBackendRequest({ method: 'PUT', url: '/scenegraphs/start', data: graph }, app, state)
  assert.equal(saveResponse.data.success, true)
  assert.equal(saveResponse.data.data.nodes[0].data.code, code)
  assert.equal(saveResponse.data.data.nodes[0].data.scriptRef, 'Assets/CustomScripts/custom-event-1.csx')

  const projectRoot = path.join(root, 'Custom Event Test')
  const scriptPath = path.join(projectRoot, 'Assets', 'CustomScripts', 'custom-event-1.csx')
  assert.equal(await fs.readFile(scriptPath, 'utf8'), code)

  const persistedGraph = JSON.parse(await fs.readFile(path.join(projectRoot, 'Settings', 'Editor', 'start.scenegraph.json'), 'utf8'))
  assert.equal(persistedGraph.nodes[0].data.code, undefined)
  assert.equal(persistedGraph.nodes[0].data.scriptRef, 'Assets/CustomScripts/custom-event-1.csx')

  const reopenedState = createBackendState()
  await handleBackendRequest({
    method: 'POST',
    url: '/project/import',
    data: { projectPath: projectRoot },
  }, app, reopenedState)
  const reopenedGraph = await handleBackendRequest({ method: 'GET', url: '/scenegraphs/start' }, app, reopenedState)
  assert.equal(reopenedGraph.data.data.nodes[0].data.code, code)
})