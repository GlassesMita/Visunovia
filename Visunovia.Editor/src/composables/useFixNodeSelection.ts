import { onMounted, onUnmounted, nextTick } from 'vue'

/**
 * 修复 BaklavaJS 节点选中行为
 *
 * 问题：BaklavaJS 内置的 selectNode 函数虽然会清空选择（当 Ctrl 未按下时），
 * 但当用户从一个节点拖动到另一个节点时，startDrag 会在同一 pointerdown 事件中
 * 被触发，导致多个节点被同时选中或拖动。
 *
 * 修复方案：
 * 1. 拦截节点 pointerdown 事件，确保在 BaklavaJS 处理前先清空所有选择
 * 2. 仅当 Ctrl 未按下时才清空（Ctrl 用于多选，保持原有行为）
 * 3. 使用事件捕获阶段（capture: true）确保优先于 BaklavaJS 的内置处理器执行
 *
 * 效果：左键点击节点 → 将节点加入当前选择，可直接多选
 *       左键点击背景 → 清空选择并平移画布
 */
export function useFixNodeSelection(viewModel: any | null) {
  const getGraph = () => viewModel?.displayedGraph || viewModel?.viewModel?.displayedGraph || null
  let editorEl: HTMLElement | null = null
  let selectionBoxEl: HTMLDivElement | null = null
  let isBoxSelecting = false
  let selectionStart = { x: 0, y: 0 }
  let selectionEnd = { x: 0, y: 0 }

  function isInputElement(target: HTMLElement): boolean {
    const tagName = target.tagName
    return tagName === 'INPUT' || tagName === 'TEXTAREA' || tagName === 'SELECT' || target.isContentEditable
  }

  function onNodePointerDown(e: PointerEvent) {
    if (e.button !== 0) return

    const target = e.target as HTMLElement
    if (isInputElement(target)) return

    const nodeEl = target.closest('.baklava-node') as HTMLElement | null
    if (!nodeEl) return

    const graph = getGraph()
    if (!graph?.selectedNodes) return

    const previousSelection = [...graph.selectedNodes]

    // Baklava 内置 selectNode 会在未按 Ctrl/Shift 时清空选择。
    // 在同一事件循环结束后恢复已有选择，实现普通左键累加多选。
    window.setTimeout(() => {
      const currentGraph = getGraph()
      if (!currentGraph?.selectedNodes) return

      const clickedNode = currentGraph.nodes?.find((node: any) => node.id === nodeEl.id)
      const merged = [...previousSelection]

      if (clickedNode && !merged.includes(clickedNode)) {
        merged.push(clickedNode)
      }

      for (const node of currentGraph.selectedNodes) {
        if (!merged.includes(node)) {
          merged.push(node)
        }
      }

      currentGraph.selectedNodes = merged
    }, 0)
  }

  function getEditorCoordinates(ev: PointerEvent) {
    const rect = editorEl!.getBoundingClientRect()
    return {
      x: ev.clientX - rect.left,
      y: ev.clientY - rect.top,
    }
  }

  function updateSelectionBox() {
    if (!selectionBoxEl) return

    const left = Math.min(selectionStart.x, selectionEnd.x)
    const top = Math.min(selectionStart.y, selectionEnd.y)
    const width = Math.abs(selectionEnd.x - selectionStart.x)
    const height = Math.abs(selectionEnd.y - selectionStart.y)

    selectionBoxEl.style.left = `${left}px`
    selectionBoxEl.style.top = `${top}px`
    selectionBoxEl.style.width = `${width}px`
    selectionBoxEl.style.height = `${height}px`
  }

  function ensureSelectionBox() {
    if (!editorEl || selectionBoxEl) return

    selectionBoxEl = document.createElement('div')
    selectionBoxEl.className = 'selection-box visunovia-selection-box'
    selectionBoxEl.style.position = 'absolute'
    selectionBoxEl.style.pointerEvents = 'none'
    selectionBoxEl.style.zIndex = '9999'
    editorEl.appendChild(selectionBoxEl)
  }

  function removeSelectionBox() {
    selectionBoxEl?.remove()
    selectionBoxEl = null
  }

  function isBoxSelectStart(ev: PointerEvent) {
    if (!editorEl || ev.button !== 0) return false

    const target = ev.target as HTMLElement
    if (isInputElement(target)) return false

    if (target.closest('.baklava-node') || target.closest('.baklava-node-palette') || target.closest('.baklava-toolbar')) {
      return false
    }

    // Baklava 的工具栏按钮/快捷键 B 会给编辑器添加该 class；Shift+左键拖拽也作为直接框选入口。
    return editorEl.classList.contains('--start-selection-box') || ev.shiftKey
  }

  function onBoxPointerDown(ev: PointerEvent) {
    if (!isBoxSelectStart(ev)) return

    ev.preventDefault()
    if (ev.shiftKey && !editorEl?.classList.contains('--start-selection-box')) {
      ev.stopImmediatePropagation()
    }

    isBoxSelecting = true
    selectionStart = getEditorCoordinates(ev)
    selectionEnd = selectionStart
    ensureSelectionBox()
    updateSelectionBox()

    document.addEventListener('pointermove', onBoxPointerMove, true)
    document.addEventListener('pointerup', onBoxPointerUp, true)
  }

  function onBoxPointerMove(ev: PointerEvent) {
    if (!isBoxSelecting || !editorEl) return

    ev.preventDefault()
    selectionEnd = getEditorCoordinates(ev)
    updateSelectionBox()
  }

  function onBoxPointerUp(ev: PointerEvent) {
    if (!isBoxSelecting || !editorEl) return

    ev.preventDefault()

    document.removeEventListener('pointermove', onBoxPointerMove, true)
    document.removeEventListener('pointerup', onBoxPointerUp, true)

    selectionEnd = getEditorCoordinates(ev)
    const graph = getGraph()
    const editorRect = editorEl.getBoundingClientRect()
    const selectionRect = {
      left: Math.min(selectionStart.x, selectionEnd.x),
      top: Math.min(selectionStart.y, selectionEnd.y),
      right: Math.max(selectionStart.x, selectionEnd.x),
      bottom: Math.max(selectionStart.y, selectionEnd.y),
    }

    if (graph?.nodes && graph?.selectedNodes) {
      graph.selectedNodes = graph.nodes.filter((node: any) => {
        const nodeEl = document.getElementById(node.id)
        if (!nodeEl) return false

        const rect = nodeEl.getBoundingClientRect()
        const nodeRect = {
          left: rect.left - editorRect.left,
          top: rect.top - editorRect.top,
          right: rect.right - editorRect.left,
          bottom: rect.bottom - editorRect.top,
        }

        return selectionRect.left < nodeRect.right
          && selectionRect.right > nodeRect.left
          && selectionRect.top < nodeRect.bottom
          && selectionRect.bottom > nodeRect.top
      })
    }

    isBoxSelecting = false
    editorEl.classList.remove('--start-selection-box')
    removeSelectionBox()
  }

  function preventBrowserMouseGestures(ev: Event) {
    const mouseEvent = ev as MouseEvent
    const isRightButton = mouseEvent.button === 2 || (mouseEvent.buttons & 2) === 2
    if (!isRightButton && ev.type !== 'contextmenu' && ev.type !== 'auxclick') return

    const target = ev.target as HTMLElement | null
    if (!target?.closest('.baklava-editor-wrapper')) return

    ev.preventDefault()
  }

  onMounted(() => {
    if (!viewModel) return

    // 等待编辑器 DOM 渲染完成后，添加事件监听
    nextTick(() => {
      editorEl = document.querySelector('.baklava-editor')
      if (editorEl) {
        // 使用 capture: true 确保在子元素事件之前触发
        editorEl.addEventListener('pointerdown', onBoxPointerDown as EventListener, true)
        editorEl.addEventListener('pointerdown', onNodePointerDown as EventListener, true)
      }
    })

    document.addEventListener('pointerdown', preventBrowserMouseGestures, true)
    document.addEventListener('pointermove', preventBrowserMouseGestures, true)
    document.addEventListener('contextmenu', preventBrowserMouseGestures, true)
    document.addEventListener('auxclick', preventBrowserMouseGestures, true)

  })

  onUnmounted(() => {
    if (editorEl) {
      editorEl.removeEventListener('pointerdown', onBoxPointerDown as EventListener, true)
      editorEl.removeEventListener('pointerdown', onNodePointerDown as EventListener, true)
    }
    document.removeEventListener('pointerdown', preventBrowserMouseGestures, true)
    document.removeEventListener('pointermove', preventBrowserMouseGestures, true)
    document.removeEventListener('contextmenu', preventBrowserMouseGestures, true)
    document.removeEventListener('auxclick', preventBrowserMouseGestures, true)
    document.removeEventListener('pointermove', onBoxPointerMove, true)
    document.removeEventListener('pointerup', onBoxPointerUp, true)
    removeSelectionBox()
  })
}
