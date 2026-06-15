<template>
  <div
    ref="rootRef"
    class="native-free-select"
    :class="{ open: isOpen, disabled }"
    :title="title || selectedLabel"
    role="listbox"
    :aria-disabled="disabled ? 'true' : 'false'"
    :tabindex="disabled ? -1 : 0"
    @click="toggleOpen"
    @keydown="handleKeydown"
  >
    <div class="native-free-select__value">
      <span>{{ selectedLabel }}</span>
      <span class="native-free-select__arrow">▾</span>
    </div>
    <div v-if="isOpen" class="native-free-select__dropdown">
      <div
        v-for="option in options"
        :key="option.value"
        class="native-free-select__option"
        :class="{ selected: option.value === modelValue, disabled: option.disabled }"
        role="option"
        :aria-selected="option.value === modelValue ? 'true' : 'false'"
        @click.stop="selectOption(option)"
      >
        {{ option.label }}
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'

export interface NativeFreeSelectOption {
  value: string
  label: string
  disabled?: boolean
}

const props = withDefaults(defineProps<{
  modelValue: string
  options: NativeFreeSelectOption[]
  disabled?: boolean
  placeholder?: string
  title?: string
}>(), {
  disabled: false,
  placeholder: '',
  title: '',
})

const emit = defineEmits<{
  (e: 'update:modelValue', value: string): void
  (e: 'change', value: string): void
}>()

const rootRef = ref<HTMLElement | null>(null)
const isOpen = ref(false)

const selectedLabel = computed(() => {
  const selected = props.options.find(option => option.value === props.modelValue)
  return selected?.label || props.placeholder || props.modelValue || ''
})

watch(() => props.disabled, (disabled) => {
  if (disabled) closeDropdown()
})

function addOutsideListener() {
  document.addEventListener('pointerdown', handleOutsidePointerDown, true)
}

function removeOutsideListener() {
  document.removeEventListener('pointerdown', handleOutsidePointerDown, true)
}

function openDropdown() {
  if (props.disabled) return
  isOpen.value = true
  addOutsideListener()
}

function closeDropdown() {
  isOpen.value = false
  removeOutsideListener()
}

function toggleOpen() {
  if (props.disabled) return
  isOpen.value ? closeDropdown() : openDropdown()
}

function handleOutsidePointerDown(event: PointerEvent) {
  if (!rootRef.value?.contains(event.target as Node)) {
    closeDropdown()
  }
}

function selectOption(option: NativeFreeSelectOption) {
  if (props.disabled || option.disabled) return
  emit('update:modelValue', option.value)
  emit('change', option.value)
  closeDropdown()
}

function moveSelection(direction: 1 | -1) {
  const enabledOptions = props.options.filter(option => !option.disabled)
  if (enabledOptions.length === 0) return

  const currentIndex = enabledOptions.findIndex(option => option.value === props.modelValue)
  const nextIndex = currentIndex < 0
    ? 0
    : (currentIndex + direction + enabledOptions.length) % enabledOptions.length
  selectOption(enabledOptions[nextIndex])
}

function handleKeydown(event: KeyboardEvent) {
  if (props.disabled) return

  if (event.key === 'Enter' || event.key === ' ') {
    event.preventDefault()
    toggleOpen()
  } else if (event.key === 'Escape') {
    event.preventDefault()
    closeDropdown()
  } else if (event.key === 'ArrowDown') {
    event.preventDefault()
    if (!isOpen.value) openDropdown()
    moveSelection(1)
  } else if (event.key === 'ArrowUp') {
    event.preventDefault()
    if (!isOpen.value) openDropdown()
    moveSelection(-1)
  }
}

onBeforeUnmount(removeOutsideListener)
</script>

<style scoped>
.native-free-select {
  position: relative;
  display: block;
  width: 100%;
  min-width: 0;
  background: #1f1f2b;
  border: 1px solid #3f3f46;
  border-radius: 4px;
  color: #e5e7eb;
  cursor: pointer;
  user-select: none;
}

.native-free-select.disabled {
  cursor: not-allowed;
  opacity: 0.5;
}

.native-free-select:focus {
  outline: none;
  border-color: #3b82f6;
}

.native-free-select__value {
  display: flex;
  align-items: center;
  justify-content: space-between;
  width: 100%;
  min-height: 24px;
  padding: 3px 6px;
  gap: 8px;
}

.native-free-select__value span:first-child {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.native-free-select__arrow {
  flex: 0 0 auto;
  color: #a1a1aa;
  font-size: 10px;
  line-height: 1;
}

.native-free-select__dropdown {
  position: absolute;
  z-index: 10020;
  top: calc(100% + 2px);
  left: 0;
  right: 0;
  max-height: 220px;
  overflow-y: auto;
  background: #111827;
  border: 1px solid #334155;
  border-radius: 4px;
  box-shadow: 0 10px 24px rgba(0, 0, 0, 0.45);
}

.native-free-select__option {
  padding: 5px 8px;
  color: #e5e7eb;
  background: #111827;
  cursor: pointer;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.native-free-select__option:hover,
.native-free-select__option.selected {
  background: #1f2937;
  color: #ffffff;
}

.native-free-select__option.disabled {
  opacity: 0.55;
  cursor: not-allowed;
}
</style>
