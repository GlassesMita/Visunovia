import { onMounted, onUnmounted, nextTick } from 'vue'
import type { Editor } from '@baklavajs/core'

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
 * 效果：单击节点 → 仅选中该节点（清空其他选择）→ 可正常拖动
 *       Ctrl+单击节点 → 添加到多选 → 可多选多节点
 */
export function useFixNodeSelection(editor: Editor | null) {
  let isCtrlPressed = false

  function onKeyDown(e: KeyboardEvent) {
    if (e.key === 'Control' || e.key === 'Meta') {
      isCtrlPressed = true
    }
  }

  function onKeyUp(e: KeyboardEvent) {
    if (e.key === 'Control' || e.key === 'Meta') {
      isCtrlPressed = false
    }
  }

  function onNodePointerDown(e: PointerEvent) {
    // 仅当 Ctrl/Meta 未按下时，在 BaklavaJS 处理前清空所有选择
    // 这样 clickNode 会变成唯一的选中节点
    if (!isCtrlPressed) {
      const target = e.target as HTMLElement
      // 确认点击的是节点元素（.baklava-node）
      if (target.closest('.baklava-node')) {
        // 清空 displayedGraph.selectedNodes
        // 通过 editor.viewModel 获取 selectedNodes
        const vm = (editor as any)?.viewModel
        if (vm?.displayedGraph?.selectedNodes) {
          vm.displayedGraph.selectedNodes = []
        }
      }
    }
  }

  onMounted(() => {
    if (!editor) return

    // 等待编辑器 DOM 渲染完成后，添加事件监听
    nextTick(() => {
      const editorEl = document.querySelector('.baklava-editor')
      if (editorEl) {
        // 使用 capture: true 确保在子元素事件之前触发
    editorEl.addEventListener('pointerdown', onNodePointerDown as EventListener, true)
      }
    })

    // 监听键盘状态
    document.addEventListener('keydown', onKeyDown)
    document.addEventListener('keyup', onKeyUp)
  })

  onUnmounted(() => {
    const editorEl = document.querySelector('.baklava-editor')
    if (editorEl) {
      editorEl.removeEventListener('pointerdown', onNodePointerDown as EventListener, true)
    }
    document.removeEventListener('keydown', onKeyDown)
    document.removeEventListener('keyup', onKeyUp)
  })
}
