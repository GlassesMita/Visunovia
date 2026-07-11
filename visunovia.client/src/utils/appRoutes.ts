export function resolveAppRoute(path: string) {
  const normalizedPath = path.startsWith('/') ? path : `/${path}`
  if (window.location.protocol === 'visunovia:') {
    return `visunovia://app/#${normalizedPath}`
  }
  return normalizedPath
}