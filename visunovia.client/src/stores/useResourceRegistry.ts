import { defineStore } from 'pinia'
import { ref, computed } from 'vue'

/**
 * 支持的资源类型枚举
 * 每种类型对应特定的连线颜色和文件扩展名过滤
 */
export type ResourceType = 
  | 'image'    // 图片/背景/Sprite
  | 'audio'    // 音效 SFX
  | 'bgm'      // 背景音乐
  | 'voice'    // 角色语音
  | 'video'    // 视频
  | 'scene'    // 场景文件
  | 'font'     // 字体文件
  | 'data'     // 数据/配置

/**
 * 资源条目接口
 */
export interface ResourceEntry {
  guid: string           // 全局唯一标识符 (UUID v4)
  nodeId: string         // 所属 BaklavaJS 节点 ID
  type: ResourceType     // 资源类型
  name: string           // 资源显示名称
  path: string           // 文件完整路径（空字符串表示未选择）
  createdAt: number       // 创建时间戳 (Date.now())
}

/**
 * 资源类型 → 连线颜色映射表
 */
export const RESOURCE_TYPE_COLORS: Record<ResourceType, string> = {
  image: '#E91E63',   // 粉红
  audio: '#FF5722',   // 深橙
  bgm:   '#795548',   // 棕色
  voice: '#607D8B',   // 蓝灰
  video: '#3F51B5',   // 靛蓝
  scene: '#009688',   // 青绿
  font:  '#8BC34A',   // 浅绿
  data:  '#9E9E9E',   // 灰
}

/**
 * 资源类型 → 文件扩展名过滤映射
 */
export const RESOURCE_TYPE_EXTENSIONS: Record<ResourceType, string[]> = {
  image: ['.png', '.jpg', '.jpeg', '.webp', '.gif', '.svg', '.bmp', '.ico'],
  audio: ['.mp3', '.wav', '.ogg', '.flac', '.aac'],
  bgm:   ['.mp3', '.wav', '.ogg', '.flac', '.m4a'],
  voice: ['.mp3', '.wav', '.ogg', '.flac', '.opus'],
  video: ['.mp4', '.webm', '.avi', '.mov', '.mkv'],
  scene: ['.json', '.scene'],
  font:  ['.ttf', '.otf', '.woff', '.woff2', '.eot'],
  data:  ['.json', '.xml', '.csv', '.txt'],
}

/**
 * 生成全局唯一 GUID (UUID v4)
 */
function generateGuid(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID()
  }
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0
    const v = c === 'x' ? r : (r & 0x3) | 0x8
    return v.toString(16)
  })
}

export const useResourceRegistry = defineStore('resourceRegistry', () => {
  /** 所有已注册的资源条目，以 nodeId 为索引 */
  const entries = ref<Map<string, ResourceEntry>>(new Map())

  /** 根据 nodeId 获取资源条目 */
  function getByNodeId(nodeId: string): ResourceEntry | undefined {
    return entries.value.get(nodeId)
  }

  /** 根据 guid 获取资源条目 */
  function getByGuid(guid: string): ResourceEntry | undefined {
    for (const entry of entries.value.values()) {
      if (entry.guid === guid) return entry
    }
    return undefined
  }

  /**
   * 注册新资源（节点创建时调用）
   * @returns 新创建的 ResourceEntry
   */
  function registerResource(nodeId: string, type: ResourceType = 'image'): ResourceEntry {
    const existing = entries.value.get(nodeId)
    if (existing) {
      existing.type = type
      return existing
    }
    
    const entry: ResourceEntry = {
      guid: generateGuid(),
      nodeId,
      type,
      name: '',
      path: '',
      createdAt: Date.now(),
    } as any
    
    entries.value.set(nodeId, entry)
    return entry
  }

  /**
   * 更新资源信息（选择文件或改名后调用）
   */
  function updateResource(nodeId: string, updates: Partial<Pick<ResourceEntry, 'name' | 'path' | 'type'>>): boolean {
    const entry = entries.value.get(nodeId)
    if (!entry) return false
    
    Object.assign(entry, updates)
    ;(entry as any).updatedAt = Date.now()
    return true
  }

  /**
   * 移除资源条目（节点删除时调用）
   */
  function removeResource(nodeId: string): boolean {
    return entries.value.delete(nodeId)
  }

  /**
   * 检查两种资源类型是否兼容（可连接）
   * 同类型始终兼容；不同类型的兼容性规则可在此扩展
   */
  function canConnect(sourceType: ResourceType, targetType: ResourceType): boolean {
    // 目前仅允许相同类型连接
    // 未来可在此添加更复杂的兼容性规则
    return sourceType === targetType
  }

  /**
   * 获取指定类型的所有资源
   */
  function getByType(type: ResourceType): ResourceEntry[] {
    const result: ResourceEntry[] = []
    for (const entry of entries.value.values()) {
      if (entry.type === type) result.push(entry)
    }
    return result
  }

  /**
   * 获取所有条目的数组形式（用于序列化）
   */
  function getAllEntries(): ResourceEntry[] {
    return Array.from(entries.value.values())
  }

  /**
   * 从数组恢复所有条目（用于反序列化）
   */
  function restoreEntries(data: ResourceEntry[]): void {
    entries.value.clear()
    for (const entry of data) {
      entries.value.set(entry.nodeId, entry)
    }
  }

  /**
   * 清空所有条目
   */
  function clear(): void {
    entries.value.clear()
  }

  /** 资源总数 */
  const totalCount = computed(() => entries.value.size)

  return {
    entries,
    totalCount,
    getByNodeId,
    getByGuid,
    registerResource,
    updateResource,
    removeResource,
    canConnect,
    getByType,
    getAllEntries,
    restoreEntries,
    clear,
  }
})

