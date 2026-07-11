const http = require('node:http')
const fs = require('node:fs')
const fsp = require('node:fs/promises')
const path = require('node:path')
const { handleBackendRequest, createBackendState, resolveProjectResourcePath, getContentType } = require('./backend.cjs')

const API_PREFIX = '/api'

function readJsonBody(request) {
  return new Promise((resolve, reject) => {
    const chunks = []
    request.on('data', chunk => chunks.push(chunk))
    request.on('end', () => {
      if (chunks.length === 0) {
        resolve(undefined)
        return
      }

      const text = Buffer.concat(chunks).toString('utf8')
      if (!text) {
        resolve(undefined)
        return
      }

      try {
        resolve(JSON.parse(text))
      } catch (error) {
        reject(error)
      }
    })
    request.on('error', reject)
  })
}

function parseContentDisposition(value) {
  const result = {}
  for (const part of String(value || '').split(';')) {
    const [rawKey, ...rawValue] = part.trim().split('=')
    if (!rawKey || rawValue.length === 0) continue
    result[rawKey] = rawValue.join('=').replace(/^"|"$/g, '')
  }
  return result
}

function readMultipartBody(request, contentType) {
  return new Promise((resolve, reject) => {
    const boundaryMatch = contentType.match(/boundary=(?:(?:"([^"]+)")|([^;]+))/i)
    const boundary = boundaryMatch?.[1] || boundaryMatch?.[2]
    if (!boundary) {
      reject(new Error('multipart boundary 缺失'))
      return
    }

    const chunks = []
    request.on('data', chunk => chunks.push(chunk))
    request.on('end', () => {
      const body = Buffer.concat(chunks)
      const delimiter = Buffer.from(`--${boundary}`)
      const fields = {}
      const files = {}
      let offset = 0

      while (offset < body.length) {
        const partStart = body.indexOf(delimiter, offset)
        if (partStart < 0) break
        const contentStart = partStart + delimiter.length
        if (body.slice(contentStart, contentStart + 2).toString() === '--') break
        const headerStart = contentStart + 2
        const headerEnd = body.indexOf(Buffer.from('\r\n\r\n'), headerStart)
        if (headerEnd < 0) break
        const nextPart = body.indexOf(delimiter, headerEnd + 4)
        if (nextPart < 0) break

        const headerText = body.slice(headerStart, headerEnd).toString('utf8')
        const content = body.slice(headerEnd + 4, Math.max(headerEnd + 4, nextPart - 2))
        const headers = Object.fromEntries(headerText.split('\r\n').map(line => {
          const index = line.indexOf(':')
          return index >= 0 ? [line.slice(0, index).toLowerCase(), line.slice(index + 1).trim()] : ['', '']
        }).filter(([key]) => key))
        const disposition = parseContentDisposition(headers['content-disposition'])
        if (disposition.name) {
          if (disposition.filename !== undefined) {
            const file = {
              filename: disposition.filename,
              contentType: headers['content-type'] || 'application/octet-stream',
              buffer: content,
            }
            if (files[disposition.name]) {
              files[disposition.name] = Array.isArray(files[disposition.name])
                ? [...files[disposition.name], file]
                : [files[disposition.name], file]
            } else {
              files[disposition.name] = file
            }
          } else {
            fields[disposition.name] = content.toString('utf8')
          }
        }
        offset = nextPart
      }

      resolve({ fields, files })
    })
    request.on('error', reject)
  })
}

function sendJson(response, status, data) {
  response.writeHead(status, {
    'Content-Type': 'application/json; charset=utf-8',
    'Cache-Control': 'no-store',
    'Access-Control-Allow-Origin': '*',
  })
  response.end(JSON.stringify(data))
}

function sendText(response, status, text) {
  response.writeHead(status, {
    'Content-Type': 'text/plain; charset=utf-8',
    'Cache-Control': 'no-store',
    'Access-Control-Allow-Origin': '*',
  })
  response.end(text)
}

async function sendFile(response, filePath, contentType) {
  const stat = await fsp.stat(filePath)
  response.writeHead(200, {
    'Content-Type': contentType || getContentType(filePath),
    'Content-Length': stat.size,
    'Cache-Control': 'no-cache',
    'Access-Control-Allow-Origin': '*',
  })
  fs.createReadStream(filePath).pipe(response)
}

async function serveStatic(response, staticRoot, pathname) {
  const normalized = decodeURIComponent(pathname).replace(/^\/+/, '')
  const relativePath = normalized || 'index.html'
  let targetPath = path.resolve(staticRoot, relativePath)
  const root = path.resolve(staticRoot)

  if (!targetPath.startsWith(root)) {
    sendJson(response, 403, { success: false, error: 'Forbidden' })
    return
  }

  try {
    const stat = await fsp.stat(targetPath)
    if (stat.isDirectory()) targetPath = path.join(targetPath, 'index.html')
    await sendFile(response, targetPath)
  } catch {
    await sendFile(response, path.join(staticRoot, 'index.html'), 'text/html; charset=utf-8')
  }
}

async function handleApiRequest(request, response, state) {
  const url = new URL(request.url, 'http://127.0.0.1')
  const apiRoute = url.pathname.slice(API_PREFIX.length) || '/'

  if (request.method === 'GET' && apiRoute.startsWith('/resources/file/')) {
    const resourcePath = decodeURIComponent(apiRoute.slice('/resources/file/'.length))
    const filePath = resolveProjectResourcePath(state, resourcePath)
    if (!filePath) {
      sendJson(response, 404, { success: false, error: '文件不存在' })
      return
    }
    await sendFile(response, filePath)
    return
  }

  if (request.method === 'GET' && apiRoute === '/FileBrowser/preview') {
    const previewPath = url.searchParams.get('path')
    if (!previewPath) {
      sendJson(response, 400, { success: false, error: '路径参数不能为空' })
      return
    }
    await sendFile(response, path.resolve(previewPath))
    return
  }

  let body
  try {
    const contentType = request.headers['content-type'] || ''
    body = String(contentType).startsWith('multipart/form-data')
      ? await readMultipartBody(request, contentType)
      : await readJsonBody(request)
  } catch {
    sendJson(response, 400, { success: false, error: '请求体无效' })
    return
  }

  const params = Object.fromEntries(url.searchParams.entries())
  const result = await handleBackendRequest({
    method: request.method,
    url: apiRoute,
    params,
    data: body,
  }, { quit: () => process.emit('visunovia:quit') }, state)

  if (typeof result.data === 'string') {
    sendText(response, result.status, result.data)
  } else {
    sendJson(response, result.status, result.data)
  }
}

function startFrontendServer({ port = 32523, staticRoot, app, state = createBackendState() }) {
  process.on('visunovia:quit', () => app.quit())

  const server = http.createServer(async (request, response) => {
    try {
      const url = new URL(request.url, `http://127.0.0.1:${port}`)
      if (url.pathname.startsWith(API_PREFIX)) {
        await handleApiRequest(request, response, state)
        return
      }

      await serveStatic(response, staticRoot, url.pathname)
    } catch (error) {
      sendJson(response, 500, { success: false, error: error instanceof Error ? error.message : String(error) })
    }
  })

  return new Promise((resolve, reject) => {
    server.once('error', reject)
    server.listen(port, '127.0.0.1', () => {
      server.off('error', reject)
      const address = server.address()
      const actualPort = typeof address === 'object' && address ? address.port : port
      resolve({
        url: `http://127.0.0.1:${actualPort}/`,
        close: () => server.close(),
        state,
      })
    })
  })
}

module.exports = { startFrontendServer, getContentType }
