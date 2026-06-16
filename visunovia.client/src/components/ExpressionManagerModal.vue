<template>
  <Transition name="expression-manager-fade">
    <div v-if="uiStore.showExpressionManager" class="expression-manager-overlay" @click.self="uiStore.closeExpressionManager()">
      <section class="expression-manager" role="dialog" aria-modal="true" aria-labelledby="expression-manager-title">
        <header class="expression-manager-header">
          <div>
            <h2 id="expression-manager-title">{{ t('expressionManager.title', 'Expression Manager') }}</h2>
            <p>{{ t('expressionManager.descriptionPrefix', 'Create reusable expressions by dragging on the canvas and editing a timeline up to 10 seconds. Assets are read from') }} <code>Assets/Emoji/Resources</code>{{ t('expressionManager.descriptionMiddle', ', and results are saved to') }} <code>Assets/Emoji/Manifest.resona</code>.</p>
          </div>
          <button class="icon-button" type="button" :aria-label="t('expressionManager.closeAria', 'Close expression manager')" @click="uiStore.closeExpressionManager()">✕</button>
        </header>

        <div class="expression-manager-body">
          <aside class="expression-sidebar">
            <div class="sidebar-actions">
              <button class="primary-button" type="button" @click="addExpression">{{ t('expressionManager.newExpression', 'New Expression') }}</button>
              <button type="button" :disabled="isLoading" @click="loadExpressions">{{ t('common.refresh', 'Refresh') }}</button>
            </div>
            <div v-if="message" class="manager-message">{{ message }}</div>
            <button
              v-for="expression in expressions"
              :key="expression.id"
              type="button"
              class="expression-list-item"
              :class="{ active: expression.id === selectedExpressionId }"
              @click="selectExpression(expression.id)"
            >
              <strong>{{ expression.name || expression.id }}</strong>
              <span>{{ formatLayerCount(expression.layers.length) }} · {{ expression.duration }}s</span>
            </button>
          </aside>

          <main v-if="selectedExpression" class="expression-workspace">
            <div class="expression-toolbar">
              <label>
                <span>ID</span>
                <input :value="selectedExpression.id" @input="updateExpression({ id: sanitizeId(($event.target as HTMLInputElement).value) })" />
              </label>
              <label>
                <span>{{ t('common.name', 'Name') }}</span>
                <input :value="selectedExpression.name" @input="updateExpression({ name: ($event.target as HTMLInputElement).value })" />
              </label>
              <label>
                <span>{{ t('expressionManager.duration', 'Duration') }}</span>
                <input type="number" min="0.1" max="10" step="0.1" :value="selectedExpression.duration" @input="updateExpression({ duration: normalizeNumber(($event.target as HTMLInputElement).value, selectedExpression.duration, 0.1, 10) })" />
              </label>
              <button type="button" @click="duplicateExpression">{{ t('expressionManager.duplicate', 'Duplicate Result') }}</button>
              <button type="button" class="danger-button" @click="removeExpression">{{ t('common.delete', 'Delete') }}</button>
              <button type="button" class="primary-button" @click="saveExpressions">{{ t('common.save', 'Save') }}</button>
            </div>

            <div class="expression-editor-grid">
              <div class="expression-canvas-panel">
                <div
                  ref="canvasRef"
                  class="expression-canvas"
                  :style="canvasStyle"
                  @pointermove="handleCanvasPointerMove"
                  @pointerup="stopDragging"
                  @pointerleave="stopDragging"
                >
                  <div class="expression-canvas-grid"></div>
                  <div
                    v-for="layer in sortedLayers"
                    :key="layer.id"
                    class="expression-layer"
                    :class="{ active: layer.id === selectedLayerId }"
                    :style="getLayerStyle(layer)"
                    @pointerdown.stop="startLayerDrag($event, layer.id)"
                    @click.stop="selectLayer(layer.id)"
                  >
                    <img v-if="layer.image" :src="resolveAssetUrl(layer.image, 'Emoji')" :alt="layer.name" draggable="false" />
                    <span v-else>{{ t('expressionManager.chooseImage', 'Choose Image') }}</span>
                  </div>
                </div>
              </div>

              <aside class="layer-panel">
                <div class="layer-panel-header">
                  <strong>{{ t('expressionManager.layers', 'Layers') }}</strong>
                  <button type="button" @click="addLayer">+ {{ t('expressionManager.layer', 'Layer') }}</button>
                </div>
                <button
                  v-for="layer in sortedLayers"
                  :key="layer.id"
                  class="layer-list-item"
                  :class="{ active: layer.id === selectedLayerId }"
                  type="button"
                  @click="selectLayer(layer.id)"
                >
                  <span>{{ layer.name || layer.id }}</span>
                  <small>{{ layer.x }}%, {{ layer.y }}%</small>
                </button>

                <div v-if="selectedLayer" class="layer-fields">
                  <label><span>{{ t('common.name', 'Name') }}</span><input :value="selectedLayer.name" @input="updateLayer({ name: ($event.target as HTMLInputElement).value })" /></label>
                  <label><span>{{ t('expressionManager.image', 'Image') }}</span><select :value="selectedLayer.image" @change="updateLayer({ image: ($event.target as HTMLSelectElement).value })"><option value="">{{ t('common.notSelected', 'Not selected') }}</option><option v-for="file in imageFiles" :key="file.path" :value="file.path">{{ file.name }}</option></select></label>
                  <div class="field-grid">
                    <label><span>X%</span><input type="number" min="0" max="100" step="1" :value="selectedLayer.x" @input="updateLayer({ x: normalizePercent(($event.target as HTMLInputElement).value, selectedLayer.x) })" /></label>
                    <label><span>Y%</span><input type="number" min="0" max="100" step="1" :value="selectedLayer.y" @input="updateLayer({ y: normalizePercent(($event.target as HTMLInputElement).value, selectedLayer.y) })" /></label>
                    <label><span>{{ t('expressionManager.widthPercent', 'Width%') }}</span><input type="number" min="1" max="200" step="1" :value="selectedLayer.width" @input="updateLayer({ width: normalizeNumber(($event.target as HTMLInputElement).value, selectedLayer.width, 1, 200) })" /></label>
                    <label><span>{{ t('expressionManager.heightPercent', 'Height%') }}</span><input type="number" min="1" max="200" step="1" :value="selectedLayer.height" @input="updateLayer({ height: normalizeNumber(($event.target as HTMLInputElement).value, selectedLayer.height, 1, 200) })" /></label>
                    <label><span>{{ t('expressionManager.rotation', 'Rotation') }}</span><input type="number" min="-360" max="360" step="1" :value="selectedLayer.rotation" @input="updateLayer({ rotation: normalizeNumber(($event.target as HTMLInputElement).value, selectedLayer.rotation, -360, 360) })" /></label>
                    <label><span>{{ t('expressionManager.opacity', 'Opacity') }}</span><input type="number" min="0" max="100" step="1" :value="selectedLayer.opacity" @input="updateLayer({ opacity: normalizePercent(($event.target as HTMLInputElement).value, selectedLayer.opacity) })" /></label>
                  </div>
                  <button type="button" @click="addKeyframe">{{ t('expressionManager.addCurrentKeyframe', 'Add keyframe at current time') }}</button>
                  <button type="button" :disabled="!selectedKeyframe" @click="removeSelectedKeyframe">{{ t('expressionManager.removeSelectedKeyframe', 'Delete selected keyframe') }}</button>
                  <button type="button" class="danger-button" @click="removeLayer">{{ t('expressionManager.deleteLayer', 'Delete layer') }}</button>
                  <div v-if="selectedKeyframe" class="bezier-fields">
                    <strong>{{ t('expressionManager.keyframeBezier', 'Keyframe Bezier Curve') }}</strong>
                    <select :value="selectedKeyframe.easing" @change="updateSelectedKeyframe({ easing: ($event.target as HTMLSelectElement).value })">
                      <option value="linear">Linear</option>
                      <option value="ease">Ease</option>
                      <option value="ease-in">Ease In</option>
                      <option value="ease-out">Ease Out</option>
                      <option value="ease-in-out">Ease In Out</option>
                      <option value="bezier">{{ t('expressionManager.customBezier', 'Custom Bezier') }}</option>
                    </select>
                    <div class="field-grid">
                      <label><span>X1</span><input type="number" min="0" max="1" step="0.01" :value="selectedKeyframe.bezierX1" @input="updateSelectedKeyframe({ bezierX1: normalizeNumber(($event.target as HTMLInputElement).value, selectedKeyframe.bezierX1, 0, 1) })" /></label>
                      <label><span>Y1</span><input type="number" min="-2" max="2" step="0.01" :value="selectedKeyframe.bezierY1" @input="updateSelectedKeyframe({ bezierY1: normalizeNumber(($event.target as HTMLInputElement).value, selectedKeyframe.bezierY1, -2, 2) })" /></label>
                      <label><span>X2</span><input type="number" min="0" max="1" step="0.01" :value="selectedKeyframe.bezierX2" @input="updateSelectedKeyframe({ bezierX2: normalizeNumber(($event.target as HTMLInputElement).value, selectedKeyframe.bezierX2, 0, 1) })" /></label>
                      <label><span>Y2</span><input type="number" min="-2" max="2" step="0.01" :value="selectedKeyframe.bezierY2" @input="updateSelectedKeyframe({ bezierY2: normalizeNumber(($event.target as HTMLInputElement).value, selectedKeyframe.bezierY2, -2, 2) })" /></label>
                    </div>
                  </div>
                </div>
              </aside>
            </div>

            <div class="timeline-panel">
              <div class="timeline-header">
                <label>{{ t('expressionManager.currentTime', 'Current Time') }} {{ currentTime.toFixed(1) }}s</label>
                <input type="range" min="0" :max="selectedExpression.duration" step="0.1" v-model.number="currentTime" />
              </div>
              <div class="timeline-track">
                <div class="timeline-ruler"></div>
                <div
                  v-for="layer in sortedLayers"
                  :key="layer.id"
                  class="timeline-row"
                >
                  <span>{{ layer.name || layer.id }}</span>
                  <div class="timeline-keyframes">
                    <button
                      v-for="keyframe in layer.keyframes"
                      :key="`${layer.id}-${keyframe.time}`"
                      class="timeline-keyframe"
                      :class="{ active: selectedLayerId === layer.id && selectedKeyframeTime === keyframe.time }"
                      :style="{ left: `${(keyframe.time / selectedExpression.duration) * 100}%` }"
                      type="button"
                      :title="`${keyframe.time}s · ${keyframe.easing || 'bezier'}`"
                      @click="applyKeyframe(layer.id, keyframe.time)"
                    ></button>
                  </div>
                </div>
              </div>
            </div>
          </main>

          <main v-else class="empty-expression-state">
            <strong>{{ t('expressionManager.emptyTitle', 'No expression results yet') }}</strong>
            <span>{{ t('expressionManager.emptyDescription', 'Click “New Expression” to start creating.') }}</span>
          </main>
        </div>
      </section>
    </div>
  </Transition>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useUIStore } from '@/stores/useUIStore'
import { getCurrentProject, getExpressionConfig, saveExpressionConfig, type ExpressionConfigEntry, type ExpressionLayerEntry, type ExpressionKeyframeEntry } from '@/api/projectApi'
import { useExpressionStore } from '@/stores/useExpressionStore'
import { getEntries } from '@/api/fileBrowser'
import { resolveAssetUrl } from '@/utils/assetPaths'
import { useLocalization } from '@/composables/useLocalization'

const uiStore = useUIStore()
const expressionStore = useExpressionStore()
const { t } = useLocalization()
const expressions = ref<ExpressionConfigEntry[]>([])
const selectedExpressionId = ref('')
const selectedLayerId = ref('')
const selectedKeyframeTime = ref<number | null>(null)
const currentTime = ref(0)
const isLoading = ref(false)
const message = ref('')
const imageFiles = ref<Array<{ name: string; path: string }>>([])
const canvasRef = ref<HTMLElement | null>(null)
const draggingLayerId = ref('')

const selectedExpression = computed(() => expressions.value.find(item => item.id === selectedExpressionId.value) || null)
const sortedLayers = computed(() => [...(selectedExpression.value?.layers || [])].sort((a, b) => a.zIndex - b.zIndex))
const selectedLayer = computed(() => selectedExpression.value?.layers.find(layer => layer.id === selectedLayerId.value) || null)
const selectedKeyframe = computed(() => selectedLayer.value?.keyframes.find(keyframe => keyframe.time === selectedKeyframeTime.value) || null)
const canvasStyle = computed(() => ({ aspectRatio: `${selectedExpression.value?.canvasWidth || 512} / ${selectedExpression.value?.canvasHeight || 512}` }))

onMounted(() => {
  loadExpressions()
})

watch(() => uiStore.showExpressionManager, visible => {
  if (visible) loadExpressions()
})

watch(currentTime, (time) => {
  applyTimelineAtTime(time)
})

async function loadExpressions() {
  isLoading.value = true
  message.value = ''
  try {
    const config = await getExpressionConfig()
    expressions.value = config.expressions
    expressionStore.expressions = config.expressions
    selectedExpressionId.value ||= expressions.value[0]?.id || ''
    selectedLayerId.value ||= selectedExpression.value?.layers[0]?.id || ''
    await loadImageFiles()
  } catch (error: any) {
    message.value = error?.message || t('expressionManager.loadFailed', 'Failed to read expression configuration')
  } finally {
    isLoading.value = false
  }
}

async function saveExpressions() {
  try {
    await saveExpressionConfig({ expressions: expressions.value })
    expressionStore.expressions = expressions.value
    message.value = t('expressionManager.saved', 'Saved. You can copy Assets/Emoji to other projects for reuse.')
  } catch (error: any) {
    message.value = error?.message || t('expressionManager.saveFailed', 'Failed to save expression configuration')
  }
}

function formatLayerCount(count: number) {
  return t('expressionManager.layerCount', '{count} layers').replace('{count}', String(count))
}

async function loadImageFiles() {
  const currentProject = await getCurrentProject()
  const root = currentProject.data?.projectPath
  if (!root) return
  const folders = [`${root}\\Assets\\Emoji\\Resources`]
  const files: Array<{ name: string; path: string }> = []
  for (const folder of folders) {
    try {
      const result = await getEntries(folder)
      files.push(...result.entries
        .filter(entry => !entry.isDirectory && /\.(png|jpe?g|webp|gif|bmp|svg)$/i.test(entry.name))
        .map(entry => ({ name: entry.name, path: entry.path || '' }))
        .filter(entry => entry.path))
    } catch {
      // ignore missing optional folder
    }
  }
  imageFiles.value = Array.from(new Map(files.map(file => [file.path, file])).values())
}

function selectExpression(id: string) {
  selectedExpressionId.value = id
  selectedLayerId.value = selectedExpression.value?.layers[0]?.id || ''
  selectedKeyframeTime.value = null
  currentTime.value = 0
}

function sanitizeId(value: string) {
  return value.trim().replace(/[^\p{L}\p{N}_-]+/gu, '-') || `expression-${Date.now()}`
}

function normalizeNumber(value: unknown, fallback: number, min: number, max: number) {
  const numeric = Number(value)
  if (!Number.isFinite(numeric)) return fallback
  return Math.max(min, Math.min(max, Math.trunc(numeric * 100) / 100))
}

function normalizePercent(value: unknown, fallback = 50) {
  return Math.trunc(normalizeNumber(value, fallback, 0, 100))
}

function createLayer(index: number): ExpressionLayerEntry {
  return {
    id: `layer-${Date.now()}-${index}`,
    name: t('expressionManager.layerName', 'Layer {index}').replace('{index}', String(index)),
    image: imageFiles.value[0]?.path || '',
    x: 50,
    y: 50,
    width: 70,
    height: 70,
    rotation: 0,
    opacity: 100,
    zIndex: index,
    keyframes: [],
  }
}

function createDefaultKeyframe(time: number, layer: ExpressionLayerEntry): ExpressionKeyframeEntry {
  return {
    time: Math.trunc(Math.min(10, time) * 10) / 10,
    x: layer.x,
    y: layer.y,
    scale: 100,
    rotation: layer.rotation,
    opacity: layer.opacity,
    easing: 'bezier',
    bezierX1: 0.25,
    bezierY1: 0.1,
    bezierX2: 0.25,
    bezierY2: 1,
  }
}

function addExpression() {
  const id = `expression-${Date.now()}`
  const expression: ExpressionConfigEntry = {
    id,
    name: t('expressionManager.defaultExpressionName', 'New Expression'),
    duration: 2,
    canvasWidth: 512,
    canvasHeight: 512,
    layers: [createLayer(1)],
  }
  expressions.value.push(expression)
  selectedExpressionId.value = id
  selectedLayerId.value = expression.layers[0].id
  selectedKeyframeTime.value = null
}

function duplicateExpression() {
  if (!selectedExpression.value) return
  const copy = JSON.parse(JSON.stringify(selectedExpression.value)) as ExpressionConfigEntry
  copy.id = `${copy.id}-copy-${Date.now()}`
  copy.name = `${copy.name || copy.id} Copy`
  expressions.value.push(copy)
  selectedExpressionId.value = copy.id
}

function removeExpression() {
  if (!selectedExpression.value) return
  expressions.value = expressions.value.filter(item => item.id !== selectedExpressionId.value)
  selectedExpressionId.value = expressions.value[0]?.id || ''
  selectedLayerId.value = selectedExpression.value?.layers[0]?.id || ''
}

function updateExpression(updates: Partial<ExpressionConfigEntry>) {
  if (!selectedExpression.value) return
  const oldId = selectedExpression.value.id
  Object.assign(selectedExpression.value, updates)
  selectedExpression.value.duration = normalizeNumber(selectedExpression.value.duration, 2, 0.1, 10)
  if (updates.id && selectedExpressionId.value === oldId) selectedExpressionId.value = updates.id
  if (currentTime.value > selectedExpression.value.duration) currentTime.value = selectedExpression.value.duration
  selectedExpression.value.layers.forEach(layer => {
    layer.keyframes = layer.keyframes.filter(keyframe => keyframe.time <= selectedExpression.value!.duration)
  })
}

function addLayer() {
  if (!selectedExpression.value) return
  const layer = createLayer(selectedExpression.value.layers.length + 1)
  selectedExpression.value.layers.push(layer)
  selectedLayerId.value = layer.id
  selectedKeyframeTime.value = null
}

function updateLayer(updates: Partial<ExpressionLayerEntry>) {
  if (!selectedLayer.value) return
  Object.assign(selectedLayer.value, updates)
  syncSelectedKeyframeFromLayer()
}

function removeLayer() {
  if (!selectedExpression.value || !selectedLayer.value) return
  selectedExpression.value.layers = selectedExpression.value.layers.filter(layer => layer.id !== selectedLayerId.value)
  selectedLayerId.value = selectedExpression.value.layers[0]?.id || ''
  selectedKeyframeTime.value = null
}

function addKeyframe() {
  if (!selectedLayer.value) return
  const keyframe = createDefaultKeyframe(currentTime.value, selectedLayer.value)
  selectedLayer.value.keyframes = selectedLayer.value.keyframes
    .filter(item => item.time !== keyframe.time)
    .concat(keyframe)
    .sort((a, b) => a.time - b.time)
  selectedKeyframeTime.value = keyframe.time
}

function selectLayer(layerId: string) {
  selectedLayerId.value = layerId
  const matchingKeyframe = selectedLayer.value?.keyframes.find(keyframe => Math.abs(keyframe.time - currentTime.value) < 0.001)
  selectedKeyframeTime.value = matchingKeyframe?.time ?? null
}

function applyKeyframe(layerId: string, time: number) {
  const layer = selectedExpression.value?.layers.find(item => item.id === layerId)
  const keyframe = layer?.keyframes.find(item => item.time === time)
  if (!layer || !keyframe) return
  selectedLayerId.value = layerId
  currentTime.value = time
  selectedKeyframeTime.value = time
  Object.assign(layer, {
    x: keyframe.x,
    y: keyframe.y,
    rotation: keyframe.rotation,
    opacity: keyframe.opacity,
  })
}

function syncSelectedKeyframeFromLayer() {
  if (!selectedLayer.value || !selectedKeyframe.value) return
  if (Math.abs(selectedKeyframe.value.time - currentTime.value) > 0.001) return
  Object.assign(selectedKeyframe.value, {
    x: selectedLayer.value.x,
    y: selectedLayer.value.y,
    rotation: selectedLayer.value.rotation,
    opacity: selectedLayer.value.opacity,
  })
}

function applyTimelineAtTime(time: number) {
  const expression = selectedExpression.value
  if (!expression) return

  const normalizedTime = Math.trunc(Math.max(0, Math.min(expression.duration, time)) * 10) / 10
  if (selectedKeyframe.value && Math.abs(selectedKeyframe.value.time - normalizedTime) > 0.001) {
    selectedKeyframeTime.value = null
  }

  expression.layers.forEach(layer => {
    const keyframes = [...layer.keyframes].sort((a, b) => a.time - b.time)
    if (keyframes.length === 0) return

    const exact = keyframes.find(keyframe => Math.abs(keyframe.time - normalizedTime) < 0.001)
    if (exact) {
      if (layer.id === selectedLayerId.value) selectedKeyframeTime.value = exact.time
      applyKeyframeValues(layer, exact)
      return
    }

    const previous = [...keyframes].reverse().find(keyframe => keyframe.time < normalizedTime)
    const next = keyframes.find(keyframe => keyframe.time > normalizedTime)

    if (!previous && next) {
      applyKeyframeValues(layer, next)
      return
    }

    if (previous && !next) {
      applyKeyframeValues(layer, previous)
      return
    }

    if (!previous || !next) return

    const span = Math.max(0.001, next.time - previous.time)
    const localProgress = Math.max(0, Math.min(1, (normalizedTime - previous.time) / span))
    const easedProgress = evaluateKeyframeEasing(next, localProgress)
    applyKeyframeValues(layer, {
      ...next,
      x: lerp(previous.x, next.x, easedProgress),
      y: lerp(previous.y, next.y, easedProgress),
      rotation: lerp(previous.rotation, next.rotation, easedProgress),
      opacity: lerp(previous.opacity, next.opacity, easedProgress),
      scale: lerp(previous.scale, next.scale, easedProgress),
    })
  })
}

function applyKeyframeValues(layer: ExpressionLayerEntry, keyframe: ExpressionKeyframeEntry) {
  layer.x = Math.round(keyframe.x)
  layer.y = Math.round(keyframe.y)
  layer.rotation = Math.round(keyframe.rotation)
  layer.opacity = Math.round(keyframe.opacity)
}

function lerp(from: number, to: number, progress: number) {
  return from + (to - from) * progress
}

function evaluateKeyframeEasing(keyframe: ExpressionKeyframeEntry, progress: number) {
  if (keyframe.easing === 'linear') return progress
  const presets: Record<string, [number, number, number, number]> = {
    ease: [0.25, 0.1, 0.25, 1],
    'ease-in': [0.42, 0, 1, 1],
    'ease-out': [0, 0, 0.58, 1],
    'ease-in-out': [0.42, 0, 0.58, 1],
  }
  const curve = presets[keyframe.easing] || [keyframe.bezierX1, keyframe.bezierY1, keyframe.bezierX2, keyframe.bezierY2]
  return cubicBezierAt(progress, curve[0], curve[1], curve[2], curve[3])
}

function cubicBezierAt(x: number, x1: number, y1: number, x2: number, y2: number) {
  let t = x
  for (let i = 0; i < 5; i += 1) {
    const currentX = cubicBezierValue(t, 0, x1, x2, 1)
    const derivative = cubicBezierDerivative(t, 0, x1, x2, 1)
    if (Math.abs(derivative) < 0.0001) break
    t -= (currentX - x) / derivative
    t = Math.max(0, Math.min(1, t))
  }
  return Math.max(0, Math.min(1, cubicBezierValue(t, 0, y1, y2, 1)))
}

function cubicBezierValue(t: number, p0: number, p1: number, p2: number, p3: number) {
  const oneMinusT = 1 - t
  return oneMinusT ** 3 * p0 + 3 * oneMinusT ** 2 * t * p1 + 3 * oneMinusT * t ** 2 * p2 + t ** 3 * p3
}

function cubicBezierDerivative(t: number, p0: number, p1: number, p2: number, p3: number) {
  const oneMinusT = 1 - t
  return 3 * oneMinusT ** 2 * (p1 - p0) + 6 * oneMinusT * t * (p2 - p1) + 3 * t ** 2 * (p3 - p2)
}

function updateSelectedKeyframe(updates: Partial<ExpressionKeyframeEntry>) {
  if (!selectedKeyframe.value) return
  Object.assign(selectedKeyframe.value, updates)
}

function removeSelectedKeyframe() {
  if (!selectedLayer.value || selectedKeyframeTime.value === null) return
  selectedLayer.value.keyframes = selectedLayer.value.keyframes.filter(keyframe => keyframe.time !== selectedKeyframeTime.value)
  selectedKeyframeTime.value = null
}

function getLayerStyle(layer: ExpressionLayerEntry) {
  return {
    left: `${layer.x}%`,
    top: `${layer.y}%`,
    width: `${layer.width}%`,
    height: `${layer.height}%`,
    opacity: layer.opacity / 100,
    zIndex: layer.zIndex,
    transform: `translate(-50%, -50%) rotate(${layer.rotation}deg)`,
  }
}

function startLayerDrag(event: PointerEvent, layerId: string) {
  selectLayer(layerId)
  draggingLayerId.value = layerId
  ;(event.currentTarget as HTMLElement).setPointerCapture?.(event.pointerId)
  updateDraggedLayer(event)
}

function handleCanvasPointerMove(event: PointerEvent) {
  if (!draggingLayerId.value) return
  updateDraggedLayer(event)
}

function stopDragging() {
  draggingLayerId.value = ''
}

function updateDraggedLayer(event: PointerEvent) {
  const canvas = canvasRef.value
  const layer = selectedExpression.value?.layers.find(item => item.id === draggingLayerId.value)
  if (!canvas || !layer) return
  const rect = canvas.getBoundingClientRect()
  layer.x = normalizePercent(((event.clientX - rect.left) / rect.width) * 100, layer.x)
  layer.y = normalizePercent(((event.clientY - rect.top) / rect.height) * 100, layer.y)
  syncSelectedKeyframeFromLayer()
}
</script>

<style scoped>
.expression-manager-overlay {
  position: fixed;
  inset: 0;
  z-index: 30000;
  background: rgba(0, 0, 0, 0.52);
  backdrop-filter: blur(2px);
}

.expression-manager {
  width: min(1180px, calc(100vw - 36px));
  height: min(820px, calc(100vh - 36px));
  margin: 18px auto;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  border: 1px solid #3e3e42;
  border-radius: 16px;
  background: #1f1f23;
  color: #f0f0f0;
  box-shadow: 0 24px 80px rgba(0, 0, 0, 0.48);
}

.expression-manager-header {
  display: flex;
  justify-content: space-between;
  gap: 18px;
  padding: 18px 22px;
  border-bottom: 1px solid #34343a;
}

.expression-manager-header h2 { margin: 0 0 6px; font-size: 20px; }
.expression-manager-header p { margin: 0; color: #b8b8c0; font-size: 12px; }
.icon-button, button { border: 1px solid #4a4a52; border-radius: 8px; padding: 7px 10px; color: #eee; background: #2b2b31; cursor: pointer; }
button:hover { background: #373740; }
button:disabled { opacity: 0.5; cursor: not-allowed; }
.primary-button { border-color: #2563eb; background: #2563eb; }
.danger-button { border-color: #7f1d1d; background: #4a1d1d; }

.expression-manager-body { min-height: 0; flex: 1; display: grid; grid-template-columns: 240px 1fr; }
.expression-sidebar { padding: 14px; border-right: 1px solid #34343a; overflow: auto; }
.sidebar-actions { display: flex; gap: 8px; margin-bottom: 10px; }
.manager-message { margin: 8px 0; color: #fbbf24; font-size: 12px; }
.expression-list-item { width: 100%; margin-bottom: 8px; display: flex; flex-direction: column; align-items: flex-start; gap: 4px; }
.expression-list-item.active { border-color: #60a5fa; background: #1d3557; }
.expression-list-item span { color: #b8b8c0; font-size: 11px; }

.expression-workspace { min-width: 0; display: flex; flex-direction: column; overflow: hidden; }
.expression-toolbar { display: grid; grid-template-columns: 130px 1fr 90px auto auto auto; gap: 10px; padding: 12px; border-bottom: 1px solid #34343a; align-items: end; }
.expression-toolbar label, .layer-fields label { display: grid; gap: 4px; color: #cfcfd6; font-size: 11px; }
input, select { width: 100%; box-sizing: border-box; border: 1px solid #4a4a52; border-radius: 7px; padding: 7px 8px; color: #eee; background: #25252b; }
.expression-editor-grid { min-height: 0; flex: 1; display: grid; grid-template-columns: 1fr 280px; overflow: hidden; }
.expression-canvas-panel { display: flex; align-items: center; justify-content: center; padding: 18px; overflow: auto; background: #111827; }
.expression-canvas { position: relative; width: min(560px, 100%); max-height: 100%; overflow: hidden; border: 1px solid #475569; border-radius: 12px; background: radial-gradient(circle at center, #263044, #0f172a); }
.expression-canvas-grid { position: absolute; inset: 0; opacity: 0.22; background-image: linear-gradient(rgba(255,255,255,.12) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,.12) 1px, transparent 1px); background-size: 32px 32px; }
.expression-layer { position: absolute; display: flex; align-items: center; justify-content: center; border: 1px solid transparent; color: #94a3b8; user-select: none; touch-action: none; }
.expression-layer.active { border-color: #60a5fa; outline: 1px dashed rgba(96, 165, 250, 0.75); }
.expression-layer img { width: 100%; height: 100%; object-fit: contain; pointer-events: none; }
.layer-panel { padding: 12px; border-left: 1px solid #34343a; overflow: auto; }
.layer-panel-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 10px; }
.layer-list-item { width: 100%; margin-bottom: 6px; display: flex; justify-content: space-between; }
.layer-list-item.active { border-color: #60a5fa; background: #1d3557; }
.layer-fields { display: grid; gap: 8px; margin-top: 12px; }
.field-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 8px; }
.bezier-fields {
  display: grid;
  gap: 8px;
  padding: 10px;
  border: 1px solid #3e3e42;
  border-radius: 10px;
  background: rgba(15, 23, 42, 0.62);
}

.bezier-fields strong {
  color: #bfdbfe;
  font-size: 12px;
}
.timeline-panel { padding: 12px; border-top: 1px solid #34343a; background: #18181d; }
.timeline-header { display: grid; grid-template-columns: 140px 1fr; gap: 12px; align-items: center; }
.timeline-track { margin-top: 10px; display: grid; gap: 6px; }
.timeline-row { display: grid; grid-template-columns: 120px 1fr; gap: 8px; align-items: center; color: #cbd5e1; font-size: 12px; }
.timeline-keyframes { position: relative; height: 22px; border: 1px solid #3e3e42; border-radius: 999px; background: #0f172a; }
.timeline-keyframe { position: absolute; top: 50%; width: 12px; height: 12px; padding: 0; border-radius: 50%; background: #fbbf24; transform: translate(-50%, -50%); }
.timeline-keyframe.active { background: #60a5fa; box-shadow: 0 0 0 3px rgba(96, 165, 250, 0.32); }
.empty-expression-state { display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 8px; color: #94a3b8; }
.expression-manager-fade-enter-active, .expression-manager-fade-leave-active { transition: opacity .16s ease; }
.expression-manager-fade-enter-from, .expression-manager-fade-leave-to { opacity: 0; }
</style>
