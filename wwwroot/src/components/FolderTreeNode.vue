<script setup lang="ts">
import type { FolderNode } from '@/api/projectApi'

defineProps<{
  node: FolderNode
  depth: number
}>()

function formatSize(bytes: number): string {
  if (bytes === 0) return ''
  const units = ['B', 'KB', 'MB', 'GB']
  let i = 0
  let size = bytes
  while (size >= 1024 && i < units.length - 1) { size /= 1024; i++ }
  return `${size.toFixed(i > 0 ? 1 : 0)} ${units[i]}`
}
</script>

<template>
  <div class="npm-tree-node" :style="{ paddingLeft: (depth * 16) + 'px' }">
    <div class="npm-tree-item">
      <svg v-if="node.isDirectory" class="npm-tree-icon" width="14" height="14" viewBox="0 0 16 16" fill="none">
        <path d="M1 4.5C1 3.67 1.67 3 2.5 3H6L7.5 4.5H13.5C14.33 4.5 15 5.17 15 6V12.5C15 13.33 14.33 14 13.5 14H2.5C1.67 14 1 13.33 1 12.5V4.5Z" fill="#FFC107" stroke="#E0A800" stroke-width="0.5"/>
      </svg>
      <svg v-else class="npm-tree-icon" width="14" height="14" viewBox="0 0 16 16" fill="none">
        <path d="M3 1C2.45 1 2 1.45 2 2V18C2 18.55 2.45 19 3 19H15C15.55 19 16 18.55 16 18V6L12 1H3Z" fill="#90CAF9" stroke="#42A5F5" stroke-width="0.5"/>
        <path d="M12 1V5C12 5.55 12.45 6 13 6H16" stroke="#42A5F5" stroke-width="0.5"/>
      </svg>
      <span class="npm-tree-name">{{ node.name }}</span>
      <span v-if="!node.isDirectory" class="npm-tree-size">{{ formatSize(node.size) }}</span>
    </div>
    <template v-if="node.isDirectory && node.children">
      <FolderTreeNode
        v-for="child in node.children"
        :key="child.path"
        :node="child"
        :depth="depth + 1"
      />
    </template>
  </div>
</template>
