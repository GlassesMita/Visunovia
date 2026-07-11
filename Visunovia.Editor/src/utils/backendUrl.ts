let cachedBackendBaseUrl = ''

export function getBackendBaseUrlSync() {
  return cachedBackendBaseUrl
}

export async function initializeBackendBaseUrl() {
  if (window.visunoviaDesktop?.platform !== 'electron') return ''
  try {
    cachedBackendBaseUrl = await window.visunoviaDesktop.getBackendBaseUrl()
  } catch {
    cachedBackendBaseUrl = ''
  }
  return cachedBackendBaseUrl
}

export function resolveBackendUrl(path: string) {
  const rawPath = String(path || '')
  if (/^(?:https?:|data:|blob:|visunovia:)/i.test(rawPath)) return rawPath
  if (!rawPath.startsWith('/api')) return rawPath
  return cachedBackendBaseUrl ? `${cachedBackendBaseUrl}${rawPath}` : rawPath
}