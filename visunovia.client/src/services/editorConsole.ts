import { readonly, ref } from 'vue'
import type { EditorConsoleEntry } from '@/api/backendProvider'

const MAX_LOCAL_ENTRIES = 2000
const entries = ref<EditorConsoleEntry[]>([])
let installed = false
let suppressForwarding = false

function serialize(value: unknown): string {
  if (value instanceof Error) return value.stack || `${value.name}: ${value.message}`
  if (typeof value === 'string') return value
  try {
    return JSON.stringify(value, (_key, nested) => typeof nested === 'bigint' ? nested.toString() : nested)
  } catch {
    return String(value)
  }
}

function addLocalEntry(entry: EditorConsoleEntry) {
  entries.value.push(entry)
  if (entries.value.length > MAX_LOCAL_ENTRIES) {
    entries.value.splice(0, entries.value.length - MAX_LOCAL_ENTRIES)
  }
}

function append(level: EditorConsoleEntry['level'], source: string, values: unknown[]) {
  const entry: EditorConsoleEntry = {
    timestamp: new Date().toISOString(),
    level,
    source,
    message: values.map(serialize).join(' '),
  }
  if (window.visunoviaDesktop) {
    window.visunoviaDesktop.appendConsoleEntry(entry)
  } else {
    addLocalEntry(entry)
  }
}

export async function installEditorConsole() {
  if (installed) return
  installed = true

  const methods: Array<[keyof Pick<Console, 'debug' | 'info' | 'log' | 'warn' | 'error'>, EditorConsoleEntry['level']]> = [
    ['debug', 'debug'],
    ['info', 'info'],
    ['log', 'info'],
    ['warn', 'warning'],
    ['error', 'error'],
  ]
  for (const [method, level] of methods) {
    const original = console[method].bind(console)
    console[method] = (...values: unknown[]) => {
      original(...values)
      if (!suppressForwarding) append(level, 'Editor', values)
    }
  }

  window.addEventListener('error', event => append('error', 'Renderer', [event.error || event.message]))
  window.addEventListener('unhandledrejection', event => append('error', 'Promise', [event.reason]))

  if (window.visunoviaDesktop) {
    entries.value = await window.visunoviaDesktop.getConsoleEntries()
    window.visunoviaDesktop.onConsoleEntry(entry => addLocalEntry(entry))
    window.visunoviaDesktop.onConsoleCleared(() => { entries.value = [] })
  }
  append('success', 'Editor', ['Editor Console connected'])
}

export function useEditorConsole() {
  async function clear() {
    entries.value = []
    await window.visunoviaDesktop?.clearConsoleEntries()
  }

  return { entries: readonly(entries), clear }
}