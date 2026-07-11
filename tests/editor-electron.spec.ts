import { test, expect, _electron as electron } from '@playwright/test'
import fs from 'node:fs/promises'
import os from 'node:os'
import path from 'node:path'

test('starts without a project and opens one through the system dialog', async () => {
  const testRoot = await fs.mkdtemp(path.join(os.tmpdir(), 'visunovia-electron-'))
  const userData = path.join(testRoot, 'user-data')
  const projectRoot = path.join(testRoot, 'Smoke Recent Project')
  const projectFile = path.join(projectRoot, 'Project.tlor')
  await fs.mkdir(userData, { recursive: true })
  await Promise.all([
    fs.mkdir(path.join(projectRoot, 'Scripts', 'Main'), { recursive: true }),
    fs.mkdir(path.join(projectRoot, 'Assets', 'Characters'), { recursive: true }),
  ])
  await fs.writeFile(projectFile, '<?xml version="1.0"?><project><metadata><title>Smoke Recent Project</title></metadata><scenes><scene id="start" /></scenes></project>', 'utf8')
  await fs.writeFile(path.join(projectRoot, 'Scripts', 'Main', 'start.lor'), JSON.stringify({ id: 'start', dialogues: [] }), 'utf8')
  await fs.writeFile(path.join(userData, 'editor-settings.json'), JSON.stringify({
    LastProjectPath: projectFile,
    RecentProjects: [{
      name: 'Smoke Recent Project',
      path: projectFile,
      lastOpened: new Date().toISOString(),
    }],
  }), 'utf8')

  const packedEditor = process.env.VISUNOVIA_PACKED_EDITOR
  const app = await electron.launch(packedEditor
    ? { executablePath: packedEditor, args: [`--user-data-dir=${userData}`] }
    : { args: [path.resolve('apps/editor-desktop/main.cjs'), `--user-data-dir=${userData}`] })

  try {
    const welcomeWindow = await app.firstWindow()
    await expect(welcomeWindow.locator('.wm-overlay')).toBeVisible()
    await expect(welcomeWindow.locator('.wm-recent-name')).toHaveText('Smoke Recent Project')
    await expect(welcomeWindow.locator('.fb-window')).toHaveCount(0)

    await app.evaluate(({ ipcMain }, selectedPath) => {
      ipcMain.removeHandler('dialog:open')
      ipcMain.handle('dialog:open', async () => selectedPath)
    }, projectFile)
    const editorWindowPromise = app.waitForEvent('window', { timeout: 10000 })
    await welcomeWindow.getByRole('button', { name: '打开项目' }).click()
    const window = await editorWindowPromise
    await expect(window.locator('.app-layout')).toBeVisible()
    const projectError = window.locator('.project-open-error')
    if (await projectError.isVisible({ timeout: 500 })) {
      throw new Error(`Project open failed: ${await projectError.innerText()}`)
    }
    await expect(window.locator('.fb-window')).toHaveCount(0)
  } finally {
    await app.close()
    await fs.rm(testRoot, { recursive: true, force: true })
  }
})