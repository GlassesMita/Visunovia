import { onMounted, onUnmounted, nextTick } from 'vue'
import type { Editor, IConnection, Graph } from '@baklavajs/core'

/**
 * 为 BaklavaJS 编辑器的连接线添加基于源节点类型的颜色标识
 * 通过在连接线 SVG path 元素上添加 data-source-type 属性实现 CSS 着色
 *
 * 支持的节点类型及颜色映射（见 BaseNode.ts NODE_TYPE_COLORS）：
 * - StartNode: #4CAF50 (绿色) - 流程起始点
 * - EndNode: #E53935 (红色) - 流程终止点
 * - EventNode: #FF9800 (橙色) - 事件触发节点
 * - DialogueNode: #2196F3 (蓝色) - 对话内容节点
 * - BranchNode: #9C27B0 (紫色) - 条件分支节点
 * - LogicNode: #00BCD4 (青色) - 逻辑处理节点
 * - ResourceNode: #795548 (棕色) - 资源引用节点（支持动态子类型着色）
 *
 * 实现原理：
 * 1. 使用 @baklavajs/events 事件系统监听连接变化（addConnection/removeConnection）
 * 2. 通过 graph.connections 获取所有连接信息
 * 3. 将连接的源节点类型映射到对应的 DOM 元素
 * 4. 使用 MutationObserver 处理 ResourceNode 自定义属性变更（resourceType）
 *
 * 为什么保留 MutationObserver：
 * BaklavaJS 的事件系统仅能监听内置的 NodeInterface 值变化（通过 node.events.update），
 * 但 ResourceNode 的 resourceType 是自定义状态属性，不在事件系统覆盖范围内。
 * 因此对于 resourceType 这类自定义属性的变更检测，仍需依赖 DOM 观察机制。
 *
 * ResourceNode 特殊说明（重构后）：
 * - resourceType: SelectInterface，用于选择资源类型（image/audio/bgm 等）
 * - resourcePath: TextInputInterface，用于输入资源路径（新增属性，不参与颜色计算）
 * - 动态着色仅基于 resourceType，resourcePath 仅作为元数据存储
 */
export function useConnectionColors(editor: Editor | null) {
  let mutationObserver: MutationObserver | null = null
  let updateTimer: ReturnType<typeof setTimeout> | null = null

  const eventToken = {}

  // 闭包中保存 graph 引用，便于 onUnmounted 时访问
  let graphInstance: Graph | null = null

  /**
   * 根据连接 ID 或索引查找对应的 DOM 元素
   * BaklavaJS 按顺序渲染连接线，我们可以通过索引匹配
   *
   * 注意：DOM 选择器使用 '.baklava-connection'（与 styles.css 保持一致）
   */
  function findConnectionElement(connectionIndex: number): SVGPathElement | null {
    const connections = document.querySelectorAll('.baklava-connection')
    return connections[connectionIndex] as SVGPathElement ?? null
  }

  /**
   * 更新所有连接线的 data-source-type 属性
   *
   * 处理逻辑：
   * 1. 遍历所有连接，获取源节点类型
   * 2. 为每个连接线设置 data-source-type 属性（基础类型标识）
   * 3. 对 ResourceNode 进行特殊处理：根据 resourceType 设置 data-resource-type
   *    - resourceType 来自 SelectInterface 的值（image/audio/bgm 等）
   *    - resourcePath 属性不参与颜色计算，仅作为元数据存储
   */
  function updateConnectionColors() {
    if (!editor) return

    const graph = editor.graph
    const connections = Array.from(graph.connections.values())

    connections.forEach((conn, index) => {
      // 获取源节点的 ID 和类型
      const sourceNodeId = conn.from?.nodeId

      if (sourceNodeId) {
        const sourceNode = graph.findNodeById(sourceNodeId)
        const nodeType = sourceNode?.type

        if (nodeType) {
          // 找到对应的 DOM 元素并设置属性
          const el = findConnectionElement(index)

          if (el) {
            // 始终设置 data-source-type 属性（基础类型标识）
            // 用于 CSS 规则：.baklava-connection[data-source-type="xxx"]
            if (el.getAttribute('data-source-type') !== nodeType) {
              el.setAttribute('data-source-type', nodeType)
            }

            // ResourceNode 特殊处理：根据动态资源类型设置 data-resource-type
            // 重构后 ResourceNode 包含两个接口：
            // - resourceType (SelectInterface): 决定连线颜色
            // - resourcePath (TextInputInterface): 仅存储路径信息
            if (nodeType === 'ResourceNode' && sourceNode) {
              const nodeObj = sourceNode as any

              // 尝试从多个位置获取 resourceType（兼容不同版本的数据结构）
              const resourceType = nodeObj?.state?.resourceType
                || nodeObj?.options?.resourceType
                || undefined

              if (resourceType && resourceType.length > 0) {
                // 有明确的资源类型，设置 data-resource-type 用于 CSS 精确着色
                // CSS 规则：.baklava-connection[data-resource-type="image"] 等
                if (el.getAttribute('data-resource-type') !== resourceType) {
                  el.setAttribute('data-resource-type', resourceType)
                }
              } else {
                // 无资源类型时清除 data-resource-type，回退到默认的 ResourceNode 颜色
                // 此时使用 --connection-resource 变量（#795548 棕色）
                if (el.hasAttribute('data-resource-type')) {
                  el.removeAttribute('data-resource-type')
                }
              }
            } else {
              // 非 ResourceNode 节点，确保清除可能残留的 data-resource-type
              // 防止节点类型变更后颜色错误（如从 ResourceNode 改为其他类型）
              if (el.hasAttribute('data-resource-type')) {
                el.removeAttribute('data-resource-type')
              }
            }
          }
        }
      }
    })
  }

  /**
   * 使用防抖机制批量更新连接线颜色
   * 避免 DOM 频繁变化时性能问题（如快速添加/删除多个连接）
   *
   * 防抖时间：50ms，在用户体验和性能之间取得平衡
   */
  function debouncedUpdate() {
    if (updateTimer) {
      clearTimeout(updateTimer)
    }
    updateTimer = setTimeout(() => {
      updateConnectionColors()
      updateTimer = null
    }, 50)
  }

  /**
   * 强制更新（用于重要事件如节点添加/删除）
   * 与 debouncedUpdate 的区别：
   * - 立即清除待执行的防抖任务
   * - 等待 DOM 更新完成（nextTick + 100ms 延迟）
   * - 确保新创建的连接线能被正确着色
   *
   * 适用场景：addNode/removeNode 事件（可能批量创建连接）
   */
  function forceUpdate() {
    if (updateTimer) {
      clearTimeout(updateTimer)
      updateTimer = null
    }

    // 等待 DOM 更新完成
    nextTick(() => {
      setTimeout(updateConnectionColors, 100)
    })
  }

  onMounted(() => {
    if (!editor) return

    const graph = editor.graph
    graphInstance = graph // 保存 graph 引用供 onUnmounted 使用

    // 初始延迟更新：等待编辑器完全渲染后再设置颜色
    // 300ms 确保 BaklavaJS 完成首次 DOM 渲染
    setTimeout(() => {
      updateConnectionColors()
    }, 300)

    // ===== 事件订阅（连接线变化） =====
    // 使用防抖机制，避免频繁触发
    graph.events.addConnection.subscribe(
      eventToken,
      (_connection: IConnection) => {
        debouncedUpdate()
      }
    )

    graph.events.removeConnection.subscribe(
      eventToken,
      (_connection: IConnection) => {
        debouncedUpdate()
      }
    )

    // ===== 事件订阅（节点变化） =====
    // 使用强制更新机制，确保节点添加/删除后连接线立即着色
    graph.events.addNode.subscribe(eventToken, () => {
      forceUpdate()
    })

    graph.events.removeNode.subscribe(eventToken, () => {
      forceUpdate()
    })

    // 事件订阅使用 token 机制，无需手动管理取消订阅列表
    // 所有取消订阅在 onUnmounted 中通过 unsubscribe(eventToken) 统一处理

    // ===== MutationObserver 配置 =====
    // 监听 BaklavaJS 编辑器容器的 DOM 变化
    // 主要用途：检测 ResourceNode 的 resourceType 属性变更（通过 UI 交互）
    //
    // 触发条件：
    // - 新增包含 .baklava-connection 类的元素（新连接线）
    // - 新增包含 .connection-wrapper 类的元素（连接线包装器）
    // - 子树中新增了连接线元素（DOM 结构变化）
    mutationObserver = new MutationObserver((mutations) => {
      const shouldUpdate = mutations.some((mutation) => {
        return Array.from(mutation.addedNodes).some((node) => {
          if (node instanceof Element) {
            const hasConnection =
              node.classList?.contains('baklava-connection') ||
              node.classList?.contains('connection-wrapper') ||
              !!node.querySelector?.('.baklava-connection')

            return hasConnection
          }
          return false
        })
      })

      if (shouldUpdate) {
        debouncedUpdate()
      }
    })

    // 观察编辑器容器（需要确保容器已渲染）
    const editorContainer = document.querySelector('.baklava-editor')
    if (editorContainer) {
      mutationObserver.observe(editorContainer, {
        childList: true,
        subtree: true,
      })
    }
  })

  /**
   * 组件卸载时的清理工作
   * 确保所有事件订阅、观察器和定时器都被正确释放，防止内存泄漏
   */
  onUnmounted(() => {
    // 取消所有事件订阅（BaklavaJS v2 使用 token 机制）
    if (graphInstance) {
      try {
        graphInstance.events.addConnection.unsubscribe(eventToken)
        graphInstance.events.removeConnection.unsubscribe(eventToken)
        graphInstance.events.addNode.unsubscribe(eventToken)
        graphInstance.events.removeNode.unsubscribe(eventToken)
      } catch {
        // 忽略取消订阅时的错误，可能来源：事件系统已销毁或 token 无效
      }
    }

    // 断开 MutationObserver 连接
    if (mutationObserver) {
      mutationObserver.disconnect()
      mutationObserver = null
    }

    // 清除待执行的防抖定时器
    if (updateTimer) {
      clearTimeout(updateTimer)
      updateTimer = null
    }
  })

  return {
    updateConnectionColors,
    forceUpdate,
  }
}
