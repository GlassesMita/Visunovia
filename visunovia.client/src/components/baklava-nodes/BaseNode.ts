import { NodeInterface } from '@baklavajs/core'
import { tSync } from '@/services/translationService'

/**
 * 节点类型 → 颜色映射表
 * 统一管理所有节点类型的显示颜色，便于全局调整主题
 */
export const NODE_TYPE_COLORS: Record<string, string> = {
  StartNode: '#2E7D32',
  EndNode: '#C62828',
  EventNode: '#E65100',
  CustomEventNode: '#455A64',
  DialogueNode: '#1565C0',
  BranchNode: '#6A1B9A',
  LogicNode: '#00838F',
  ResourceNode: '#4E342E',
  ChoiceNode: '#C2185B',
  CharacterControlNode: '#7B1FA2',
}

/**
 * 默认端口箭头符号
 * 用于执行流端口的统一标签，保持视觉一致性
 */
export const ARROW_SYMBOL = '→'

/**
 * 创建标准执行流输入端口
 * @param label - 端口标签（默认为箭头符号）
 * @returns 端口工厂函数
 */
export function createExecInPort(label?: string): () => NodeInterface {
  return () => new NodeInterface(label || ARROW_SYMBOL, undefined).setPort(true)
}

/**
 * 创建标准执行流输出端口
 * @param label - 端口标签（默认为箭头符号）
 * @returns 端口工厂函数
 */
export function createExecOutPort(label?: string): () => NodeInterface {
  return () => new NodeInterface(label || ARROW_SYMBOL, undefined)
}

/**
 * 设置节点的国际化标题
 * 在节点 onCreate 钩子中调用，自动从 localization store 获取翻译文本
 * 
 * @param node - 节点实例（通常使用 this）
 * @param i18nKey - 国际化键名（如 'nodes.start'）
 * @param fallback - 翻译缺失时的回退文本
 */
export function setNodeI18nTitle(
  node: { title: string },
  i18nKey: string,
  fallback: string
): void {
  node.title = tSync(i18nKey, fallback)
}
