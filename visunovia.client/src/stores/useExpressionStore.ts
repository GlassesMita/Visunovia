import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { getExpressionConfig, saveExpressionConfig, type ExpressionConfigEntry } from '@/api/projectApi'

export const useExpressionStore = defineStore('expressions', () => {
  const expressions = ref<ExpressionConfigEntry[]>([])
  const isLoaded = ref(false)
  const isLoading = ref(false)

  const sortedExpressions = computed(() => [...expressions.value].sort((a, b) => (a.name || a.id).localeCompare(b.name || b.id, 'zh-CN')))

  async function refresh() {
    if (isLoading.value) return
    isLoading.value = true
    try {
      const config = await getExpressionConfig()
      expressions.value = config.expressions
      isLoaded.value = true
    } finally {
      isLoading.value = false
    }
  }

  async function load() {
    if (isLoaded.value || isLoading.value) return
    await refresh()
  }

  async function save(nextExpressions = expressions.value) {
    const saved = await saveExpressionConfig({ expressions: nextExpressions })
    expressions.value = saved.expressions
    isLoaded.value = true
    return saved.expressions
  }

  function findExpression(id: string) {
    const normalizedId = String(id || '').trim()
    return expressions.value.find(expression => expression.id === normalizedId || expression.name === normalizedId) || null
  }

  return {
    expressions,
    sortedExpressions,
    isLoaded,
    isLoading,
    load,
    refresh,
    save,
    findExpression,
  }
})
