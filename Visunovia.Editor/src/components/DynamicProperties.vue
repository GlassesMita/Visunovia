<template>
  <div class="dynamic-properties">
    <div v-for="prop in visibleProperties" :key="prop.name" class="property-row">
      <label>{{ t(`properties.${prop.name}`, prop.name) }}</label>
      
      <input 
        v-if="prop.type === 'string'" 
        type="text" 
        :value="getValue(prop.name)" 
        @input="updateValue(prop.name, ($event.target as HTMLInputElement).value)"
      />
      
      <input 
        v-else-if="prop.type === 'number'" 
        type="number" 
        :value="getValue(prop.name)" 
        @input="updateValue(prop.name, parseFloat(($event.target as HTMLInputElement).value))"
      />
      
      <select 
        v-else-if="prop.type === 'select'" 
        :value="getValue(prop.name)"
        @change="updateValue(prop.name, ($event.target as HTMLSelectElement).value)"
      >
        <option v-for="opt in prop.options" :key="opt.value" :value="opt.value">
          {{ opt.label }}
        </option>
      </select>
      
      <input 
        v-else-if="prop.type === 'boolean'" 
        type="checkbox" 
        :checked="getValue(prop.name)"
        @change="updateValue(prop.name, ($event.target as HTMLInputElement).checked)"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useLocalization } from '@/composables/useLocalization'
import { PropertyConfig } from '@/types'

const props = defineProps<{
  properties: PropertyConfig[]
  values: Record<string, any>
}>()

const emit = defineEmits<{
  (e: 'update', name: string, value: any): void
}>()

const { t } = useLocalization()

const visibleProperties = computed(() => props.properties)

function getValue(name: string): any {
  return props.values[name] ?? props.properties.find(p => p.name === name)?.defaultValue
}

function updateValue(name: string, value: any) {
  emit('update', name, value)
}
</script>

<style scoped>
.dynamic-properties {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.property-row {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.property-row label {
  font-size: 0.75rem;
  color: var(--md-sys-color-on-surface-variant);
}

.property-row input,
.property-row select {
  padding: 0.25rem 0.5rem;
  border: 1px solid var(--md-sys-color-outline-variant);
  border-radius: var(--md-sys-shape-corner-small);
  background: var(--md-sys-color-surface-container-high);
  color: var(--md-sys-color-on-surface);
  font-size: 0.875rem;
}
</style>
