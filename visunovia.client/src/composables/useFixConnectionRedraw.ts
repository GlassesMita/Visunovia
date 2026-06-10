/**
 * 强制刷新 BaklavaJS 连接线端点
 *
 * BaklavaJS 拖拽节点时会原地修改 node.position.x/y。部分情况下连接线组件的
 * watch 无法感知到这个深层变更，导致节点移动了，但已连接的 SVG 路径仍停留在原位。
 *
 * 这里在节点拖拽结束后，将被拖拽节点的 position 替换为同值的新对象，触发 Vue 对
 * 连接线端点位置的重新计算，避免拖拽期间每帧重载连接组件造成卡顿。
 */
export function useFixConnectionRedraw(baklava: any | null) {
  if (!baklava || baklava.__visunoviaConnectionRedrawFixInstalled) return

  baklava.__visunoviaConnectionRedrawFixInstalled = true

  let frameId: number | null = null
  let draggedNode: any | null = null
  let pendingRefreshNode: any | null = null

  function refreshDraggedNodePositions() {
    frameId = null

    const node = pendingRefreshNode
    pendingRefreshNode = null

    const position = node?.position
    if (position && typeof position.x === 'number' && typeof position.y === 'number') {
      node.position = { x: position.x, y: position.y }
    }

    // BaklavaJS Editor 组件会在 editor.hooks.load 执行时递增内部 counter，
    // connection key 随之变化并重新挂载 ConnectionWrapper，从而重新读取端口 DOM 坐标。
    // 这比直接修改 SVG path 更贴近渲染器自身流程，也能保持连线样式/缩放逻辑一致。
    baklava.editor?.hooks?.load?.execute({})
  }

  function scheduleRefresh() {
    pendingRefreshNode = draggedNode
    if (frameId !== null) return
    frameId = window.requestAnimationFrame(refreshDraggedNodePositions)
  }

  function onNodePointerDown(e: PointerEvent) {
    if (e.button !== 0) return

    const target = e.target as HTMLElement | null
    if (target?.closest('.baklava-node .__title')) {
      const nodeElement = target.closest('.baklava-node') as HTMLElement | null
      draggedNode = findNodeByElement(nodeElement)
    }
  }

  function findNodeByElement(nodeElement: HTMLElement | null) {
    if (!nodeElement) return null
    const vm = baklava?.viewModel ?? baklava
    const nodes = vm?.displayedGraph?.nodes ?? []
    const title = nodeElement.querySelector('.__title-label')?.textContent?.trim()
    return nodes.find((node: any) => node?.title === title) ?? null
  }

  function onPointerUp() {
    if (draggedNode) {
      scheduleRefresh()
      draggedNode = null
    }
  }

  // 该 composable 会在 BaklavaEditor 的 onMounted 内初始化，因此这里不能再使用
  // Vue 生命周期钩子注册监听器。直接监听 document，可覆盖之后才渲染出的编辑器 DOM。
  document.addEventListener('pointerdown', onNodePointerDown as EventListener, true)
  document.addEventListener('pointerup', onPointerUp)
}
