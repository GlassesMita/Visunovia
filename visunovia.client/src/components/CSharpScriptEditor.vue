<template>
  <div ref="host" class="csharp-script-editor" aria-label="C# custom event script"></div>
</template>

<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, watch } from 'vue'
import ace from 'ace-builds/src-noconflict/ace'
import 'ace-builds/src-noconflict/mode-csharp'
import 'ace-builds/src-noconflict/theme-tomorrow_night'
import 'ace-builds/src-noconflict/ext-language_tools'

const props = defineProps<{ modelValue: string }>()
const emit = defineEmits<{ 'update:modelValue': [value: string] }>()

const host = ref<HTMLElement | null>(null)
let editor: ace.Ace.Editor | null = null
let applyingExternalValue = false

onMounted(() => {
  if (!host.value) return
  editor = ace.edit(host.value)
  editor.setValue(props.modelValue || '', -1)
  editor.session.setMode('ace/mode/csharp')
  editor.setTheme('ace/theme/tomorrow_night')
  editor.setOptions({
    fontFamily: '"Cascadia Code", "Microsoft YaHei UI", "Noto Sans CJK SC", Consolas, monospace',
    fontSize: '13px',
    showPrintMargin: false,
    tabSize: 4,
    useSoftTabs: true,
    wrap: true,
    enableBasicAutocompletion: true,
    enableLiveAutocompletion: true,
  })
  editor.session.on('change', () => {
    if (!applyingExternalValue) emit('update:modelValue', editor?.getValue() || '')
  })
})

watch(() => props.modelValue, (value) => {
  if (!editor || value === editor.getValue()) return
  applyingExternalValue = true
  editor.setValue(value || '', -1)
  applyingExternalValue = false
})

onBeforeUnmount(() => editor?.destroy())
</script>

<style scoped>
.csharp-script-editor {
  min-height: 220px;
  border: 1px solid var(--md-sys-color-outline-variant, #79747e);
  border-radius: 4px;
  overflow: hidden;
}
</style>