/**
 * 运行时本地化接口 stub
 * 为 PreviewPopup 的 sceneRuntime 提供本地化查找能力
 */

export interface CoreRTLocalization {
  setLanguage(lang: string): void
  setTranslations(translations: Map<string, string>): void
  translate(key: string): string
}

export function createCoreRTLocalization(options: {
  language: string
  fallbackLanguage: string
}): CoreRTLocalization {
  let currentLang = options.language
  let fallbackLang = options.fallbackLanguage
  let translations: Map<string, string> = new Map()

  return {
    setLanguage(lang: string) {
      currentLang = lang
    },
    setTranslations(newTranslations: Map<string, string>) {
      translations = newTranslations
    },
    translate(key: string): string {
      const lowerKey = key.toLowerCase()
      return translations.get(key) || translations.get(lowerKey) || key
    },
  }
}

interface RuntimeGraphNode {
  id: string
  type: string
  data: Record<string, unknown>
  nextNodeUuids: string[]
}

interface RuntimeGraphConnection {
  id: string
  from: { nodeId: string; port: string }
  to: { nodeId: string; port: string }
}

export function normalizeRuntimeSceneGraph(input: unknown): {
  nodes: RuntimeGraphNode[]
  connections: RuntimeGraphConnection[]
} {
  const graph = input && typeof input === 'object' ? input as Record<string, unknown> : {}
  const rawConnections = Array.isArray(graph.connections)
    ? graph.connections
    : Array.isArray(graph.edges) ? graph.edges : []
  const connections = rawConnections.map((connection, index) => normalizeConnection(connection, index)).filter(Boolean) as RuntimeGraphConnection[]
  const rawNodes = Array.isArray(graph.nodes) ? graph.nodes : []
  const nodes = rawNodes.map((node) => {
    const source = node && typeof node === 'object' ? node as Record<string, unknown> : {}
    const id = String(source.id || source.uuid || '')
    return {
      id,
      type: String(source.type || source.nodeType || 'UnknownNode'),
      data: source.data && typeof source.data === 'object'
        ? source.data as Record<string, unknown>
        : source.properties && typeof source.properties === 'object' ? source.properties as Record<string, unknown> : {},
      nextNodeUuids: connections.filter(connection => connection.from.nodeId === id).map(connection => connection.to.nodeId),
    }
  }).filter(node => node.id)

  return { nodes, connections }
}

function normalizeConnection(value: unknown, index: number): RuntimeGraphConnection | null {
  if (!value || typeof value !== 'object') return null
  const connection = value as Record<string, any>
  const from = connection.from || { nodeId: connection.source || connection.sourceNodeUuid, port: connection.sourcePort }
  const to = connection.to || { nodeId: connection.target || connection.targetNodeUuid, port: connection.targetPort }
  const fromNodeId = String(from?.nodeId || '')
  const toNodeId = String(to?.nodeId || '')
  if (!fromNodeId || !toNodeId) return null
  return {
    id: String(connection.id || connection.uuid || `connection-${index}`),
    from: { nodeId: fromNodeId, port: String(from?.port || from?.name || 'execOut') },
    to: { nodeId: toNodeId, port: String(to?.port || to?.name || 'execIn') },
  }
}
