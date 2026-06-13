<template>
  <Transition name="preview-popup-fade">
    <div v-if="visible" class="preview-popup-overlay" @click.self="closePreview">
      <section class="preview-popup-dialog" role="dialog" aria-modal="true" aria-labelledby="preview-popup-title">
        <header class="preview-popup-header">
          <div>
            <h3 id="preview-popup-title">项目预览</h3>
            <p>{{ viewport.width }} × {{ viewport.height }}</p>
          </div>
          <div class="preview-popup-actions">
            <label class="preview-resolution-picker">
              <span>渲染分辨率</span>
              <select :value="selectedResolutionKey" @change="handleResolutionChange">
                <option
                  v-for="resolution in PREVIEW_RESOLUTIONS"
                  :key="resolution.key"
                  :value="resolution.key"
                >
                  {{ resolution.label }}
                </option>
              </select>
            </label>
            <div class="preview-popup-window-actions" aria-label="录制演出预览">
              <span>录制模式</span>
              <button type="button" @click="openStageModal(1920, 1080)">1080P</button>
              <button type="button" @click="openStageModal(1280, 720)">720P</button>
            </div>
            <button type="button" @click="restartPreview">重新播放</button>
            <button class="preview-popup-close" type="button" @click="closePreview">✕</button>
          </div>
        </header>

        <div class="preview-popup-body">
          <div
            class="preview-stage-shell"
            :style="stageShellStyle"
          >
            <div
              ref="stageRef"
              class="preview-stage"
              :style="stageStyle"
              @click="advanceFromStage"
            >
              <video
                v-if="currentBackground.url && currentBackground.type === 'video'"
                class="preview-background"
                :src="currentBackground.url"
                autoplay
                loop
                muted
                playsinline
                @error="logMediaError('背景视频', currentBackground.url)"
              ></video>
              <img v-else-if="currentBackground.url" class="preview-background" :src="currentBackground.url" alt="background" @error="logMediaError('背景图片', currentBackground.url)" />
              <div v-else class="preview-background-placeholder">Visunovia Preview</div>

              <div class="preview-characters">
                <img
                  v-for="character in renderableCharacters"
                  :key="character.slot"
                  class="preview-character"
                  :class="[`preview-character-${character.position}`, `preview-character-${character.animation}`]"
                  :style="getCharacterStyle(character)"
                  :src="character.spriteUrl"
                  :alt="character.character || `character-${character.slot}`"
                  @error="logMediaError('角色立绘', character.spriteUrl)"
                />
              </div>

              <div v-if="choices.length > 0" class="preview-choices" @click.stop>
                <button
                  v-for="choice in choices"
                  :key="choice.port"
                  type="button"
                  @click="selectChoice(choice.port)"
                >
                  {{ choice.text }}
                </button>
              </div>

              <div v-if="dialogueVisible" class="preview-dialogue" @click.stop="advancePreview">
                <div v-if="speaker" class="preview-speaker-row">
                  <span class="preview-speaker">{{ speaker }}</span>
                  <span v-if="speakerAffiliation" class="preview-affiliation">{{ speakerAffiliation }}</span>
                </div>
                <div class="preview-dialogue-line" aria-hidden="true"></div>
                <div class="preview-text markdown-body" v-html="renderedDialogueHtml"></div>
                <div v-if="textComplete && !previewEnded" class="preview-continue-indicator" aria-hidden="true"></div>
              </div>

              <div v-if="statusMessage" class="preview-status">{{ statusMessage }}</div>
            </div>
          </div>
        </div>

        <audio ref="bgmAudio" loop></audio>
      </section>

      <Teleport to="body">
        <div v-if="stageModalVisible" class="preview-recording-modal" @click.self="closeStageModal">
          <div class="preview-recording-launcher" role="dialog" aria-modal="true" aria-label="录制演出模式">
            <h3>录制演出模式</h3>
            <p>{{ viewport.width }} × {{ viewport.height }}</p>
            <button type="button" class="preview-recording-play" @click="startFullscreenRecordingPreview">
              ▶ 播放并进入全屏
            </button>
            <button type="button" class="preview-recording-cancel" @click="closeStageModal">取消</button>
          </div>
        </div>
      </Teleport>
    </div>
  </Transition>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, ref, watch } from 'vue'
import MarkdownIt from 'markdown-it'
import DOMPurify from 'dompurify'
import { useNodeGraphStore } from '@/stores/useNodeGraphStore'
import type { GraphConnection, GraphNode } from '@/stores/useNodeGraphStore'
import { useCharacterStore } from '@/stores/useCharacterStore'
import { resolveAssetUrl } from '@/utils/assetPaths'

type PreviewSettings = {
  previewWidth?: number
  previewHeight?: number
  fullscreenPreview?: boolean
}

type CharacterSlotState = {
  slot: string
  character: string
  sprite: string
  spriteUrl: string
  position: string
  animation: string
  fromPosition?: string
  easing?: string
  duration?: number
}

type PreviewChoice = {
  text: string
  port: string
}

type PreviewNode = {
  id: string
  type: string
  data: Record<string, any>
  nextNodeUuids: string[]
}

type PreviewConnection = {
  id: string
  from: { nodeId: string; port: string }
  to: { nodeId: string; port: string }
}

type PreviewResourceKind = 'background' | 'character' | 'bgm' | 'sfx' | 'voice'

type PreviewResourceTrace = {
  kind: PreviewResourceKind
  label: string
  nodeId: string
  nodeType: string
  field: string
  rawPath: string
  resolvedUrl: string
}

const props = defineProps<{
  visible: boolean
  reloadToken: number
}>()

const emit = defineEmits<{
  close: []
}>()

const SETTINGS_STORAGE_KEY = 'visunovia-settings'
const DEFAULT_VIEWPORT = { width: 1280, height: 720 }
const VIDEO_ASSET_EXTENSIONS = new Set(['.mp4', '.webm', '.m4v', '.mov', '.ogv'])
const PREVIEW_RESOLUTIONS = [
  { key: '2560x1440', label: '2560 × 1440', width: 2560, height: 1440 },
  { key: '1920x1080', label: '1920 × 1080', width: 1920, height: 1080 },
  { key: '1280x720', label: '1280 × 720', width: 1280, height: 720 },
]
const markdownRenderer = new MarkdownIt({
  breaks: true,
  html: true,
  linkify: true,
  typographer: true,
})
markdownRenderer.enable(['table', 'strikethrough'])
markdownRenderer.inline.ruler.before('link', 'visunovia_ruby_annotation', createRubyAnnotationRule())
markdownRenderer.inline.ruler.before('link', 'visunovia_inside_annotation', createInsideAnnotationRule())
markdownRenderer.inline.ruler.before('emphasis', 'visunovia_mark', createSimpleInlineRule('==', 'mark'))
markdownRenderer.inline.ruler.before('emphasis', 'visunovia_underline', createSimpleInlineRule('++', 'u'))

const nodeGraphStore = useNodeGraphStore()
const characterStore = useCharacterStore()
const viewport = ref({ ...DEFAULT_VIEWPORT })
const graphNodes = ref<PreviewNode[]>([])
const graphConnections = ref<PreviewConnection[]>([])
const currentNodeId = ref<string | null>(null)
const currentBackground = ref<{ url: string; type: 'image' | 'video' }>({ url: '', type: 'image' })
const speaker = ref('')
const speakerAffiliation = ref('')
const dialogueText = ref('')
const fullDialogueText = ref('')
const visibleTextLength = ref(0)
const dialogueVisible = ref(false)
const textComplete = ref(false)
const statusMessage = ref('')
const previewEnded = ref(false)
const choices = ref<PreviewChoice[]>([])
const characterSlots = ref<Record<string, CharacterSlotState>>({})
const bgmAudio = ref<HTMLAudioElement | null>(null)
const stageRef = ref<HTMLElement | null>(null)
const stageModalVisible = ref(false)
const resourceTraceByUrl = ref<Record<string, PreviewResourceTrace>>({})
let typewriterTimer: number | null = null

const visibleCharacters = computed(() => Object.values(characterSlots.value).sort((a, b) => Number(a.slot) - Number(b.slot)))
const renderableCharacters = computed(() => visibleCharacters.value.filter(character => character.slot !== '6' && Boolean(character.sprite && character.spriteUrl)))

const POSITION_LEFT: Record<string, string> = {
  left: '10%',
  center: '50%',
  right: '60%',
}

const POSITION_TRANSFORM: Record<string, string> = {
  left: 'translateX(0)',
  center: 'translateX(-50%)',
  right: 'translateX(0)',
}

const EASING_CURVES: Record<string, string> = {
  easeOutCubic: 'cubic-bezier(0.22, 1, 0.36, 1)',
  easeInOutCubic: 'cubic-bezier(0.65, 0, 0.35, 1)',
  easeOutBack: 'cubic-bezier(0.34, 1.56, 0.64, 1)',
  linear: 'linear',
}

const renderedDialogueHtml = computed(() => DOMPurify.sanitize(renderDialogueMarkdown(fullDialogueText.value, visibleTextLength.value), {
  ADD_TAGS: ['ruby', 'rp', 'rt', 'i', 'u', 'mark', 'span'],
  ADD_ATTR: ['class', 'title'],
}))

const selectedResolutionKey = computed(() => `${viewport.value.width}x${viewport.value.height}`)

const stageShellStyle = computed(() => ({
  aspectRatio: `${viewport.value.width} / ${viewport.value.height}`,
  maxWidth: `${viewport.value.width}px`,
}))

const stageStyle = computed(() => ({
  width: `${viewport.value.width}px`,
  height: `${viewport.value.height}px`,
  '--preview-width': viewport.value.width,
  '--preview-height': viewport.value.height,
}))

function createSimpleInlineRule(marker: string, tag: string) {
  return (state: any, silent: boolean) => {
    const start = state.pos
    if (!state.src.startsWith(marker, start)) return false

    const contentStart = start + marker.length
    const end = state.src.indexOf(marker, contentStart)
    if (end < 0 || end === contentStart) return false

    if (!silent) {
      const openToken = state.push(`${tag}_open`, tag, 1)
      openToken.markup = marker
      const textToken = state.push('text', '', 0)
      textToken.content = state.src.slice(contentStart, end)
      const closeToken = state.push(`${tag}_close`, tag, -1)
      closeToken.markup = marker
    }

    state.pos = end + marker.length
    return true
  }
}

function createRubyAnnotationRule() {
  return (state: any, silent: boolean) => {
    const start = state.pos
    const marker = '[Ann|'
    if (!state.src.startsWith(marker, start)) return false

    const contentStart = start + marker.length
    const separator = state.src.indexOf('|', contentStart)
    if (separator < 0) return false

    const end = state.src.indexOf(']', separator + 1)
    if (end < 0) return false

    const mainText = state.src.slice(contentStart, separator)
    const annotationText = state.src.slice(separator + 1, end)
    if (!mainText || !annotationText) return false

    if (!silent) {
      state.push('ruby_open', 'ruby', 1)
      const mainToken = state.push('text', '', 0)
      mainToken.content = mainText
      state.push('rp_open', 'rp', 1)
      const leftParenToken = state.push('text', '', 0)
      leftParenToken.content = '('
      state.push('rp_close', 'rp', -1)
      state.push('rt_open', 'rt', 1)
      const annotationToken = state.push('text', '', 0)
      annotationToken.content = annotationText
      state.push('rt_close', 'rt', -1)
      state.push('rp_open', 'rp', 1)
      const rightParenToken = state.push('text', '', 0)
      rightParenToken.content = ')'
      state.push('rp_close', 'rp', -1)
      state.push('ruby_close', 'ruby', -1)
    }

    state.pos = end + 1
    return true
  }
}

function createInsideAnnotationRule() {
  return (state: any, silent: boolean) => {
    const start = state.pos
    const marker = '[Inside|'
    if (!state.src.startsWith(marker, start)) return false

    const contentStart = start + marker.length
    const end = state.src.indexOf(']', contentStart)
    if (end < 0) return false

    const content = state.src.slice(contentStart, end)
    const separator = content.indexOf('|')
    const hiddenText = separator >= 0 ? content.slice(0, separator) : content
    const titleText = (separator >= 0 ? content.slice(separator + 1) : '').trim() || '你知道的太多了'
    if (!hiddenText) return false

    if (!silent) {
      const openToken = state.push('span_open', 'span', 1)
      openToken.attrs = [
        ['class', 'dialog-inside'],
        ['title', titleText],
      ]
      const textToken = state.push('text', '', 0)
      textToken.content = hiddenText
      state.push('span_close', 'span', -1)
    }

    state.pos = end + 1
    return true
  }
}

function normalizeDialogueEscapes(value: string) {
  return value.replace(/\\([nrt\\])/g, (_, escaped: string) => {
    const replacements: Record<string, string> = {
      n: '<br />',
      r: '',
      t: '&emsp;',
      '\\': '\\',
    }
    return replacements[escaped] ?? escaped
  })
}

function renderDialogueMarkdown(value: string, visibleLimit = Number.POSITIVE_INFINITY) {
  const rendered = renderDialogueInlineMarkup(normalizeDialogueEscapes(value), visibleLimit)
  return markdownRenderer
    .render(rendered.html)
    .replace(/<br\s*\/?>\s*\n/gi, '<br />')
}

function renderDialogueInlineMarkup(value: string, visibleLimit = Number.POSITIVE_INFINITY) {
  const initialRemaining = Number.isFinite(visibleLimit) ? visibleLimit : Number.MAX_SAFE_INTEGER
  const state = { remaining: initialRemaining }
  return { html: renderInlineSegment(value, state), consumed: initialRemaining - state.remaining }
}

function renderInlineSegment(value: string, state: { remaining: number }) {
  let output = ''
  let plain = ''
  let index = 0

  const flushPlain = () => {
    if (!plain) return
    output += markdownRenderer.renderInline(plain)
    plain = ''
  }

  while (index < value.length && state.remaining > 0) {
    const simple = parseSimpleMarkup(value, index, state)
    if (simple) {
      flushPlain()
      output += simple.html
      index = simple.end
      continue
    }

    const annotation = parseAnnotationMarkup(value, index, state)
    if (annotation) {
      flushPlain()
      output += annotation.html
      index = annotation.end
      continue
    }

    const char = readCodePoint(value, index)
    plain += char.value
    state.remaining -= 1
    index = char.end
  }

  flushPlain()
  return output
}

function parseSimpleMarkup(value: string, start: number, state: { remaining: number }) {
  const config = value.startsWith('==', start)
    ? { marker: '==', tag: 'mark' }
    : value.startsWith('++', start)
      ? { marker: '++', tag: 'u' }
      : null
  if (!config) return null

  const contentStart = start + config.marker.length
  const end = findClosingMarker(value, contentStart, config.marker)
  if (end < 0) return null

  const inner = renderInlineSegment(value.slice(contentStart, end), state)
  return {
    html: inner ? `<${config.tag}>${inner}</${config.tag}>` : '',
    end: end + config.marker.length,
  }
}

function parseAnnotationMarkup(value: string, start: number, state: { remaining: number }) {
  const type = value.startsWith('[Ann|', start) ? 'ann' : value.startsWith('[Inside|', start) ? 'inside' : ''
  if (!type) return null

  const contentStart = start + (type === 'ann' ? '[Ann|'.length : '[Inside|'.length)
  const end = findClosingBracket(value, contentStart)
  if (end < 0) return null

  const parts = splitTopLevelPipes(value.slice(contentStart, end))
  if (type === 'ann') {
    if (parts.length < 2 || !parts[0] || !parts[1]) return null
    const before = state.remaining
    const mainHtml = renderInlineSegment(parts[0], state)
    if (!mainHtml && before === state.remaining) return { html: '', end: end + 1 }
    const annotationHtml = renderInlineSegment(parts.slice(1).join('|'), { remaining: Number.POSITIVE_INFINITY })
    return {
      html: `<ruby>${mainHtml}<rp>(</rp><rt>${annotationHtml}</rt><rp>)</rp></ruby>`,
      end: end + 1,
    }
  }

  if (!parts[0]) return null
  const before = state.remaining
  const hiddenHtml = renderInlineSegment(parts[0], state)
  if (!hiddenHtml && before === state.remaining) return { html: '', end: end + 1 }
  const title = stripHtml(renderInlineSegment(parts.slice(1).join('|') || '你知道的太多了', { remaining: Number.POSITIVE_INFINITY }))
  return {
    html: `<span class="dialog-inside" title="${escapeHtmlAttribute(title)}">${hiddenHtml}</span>`,
    end: end + 1,
  }
}

function findClosingMarker(value: string, start: number, marker: string) {
  let index = start
  while (index < value.length) {
    if (value.startsWith(marker, index)) return index
    const char = readCodePoint(value, index)
    index = char.end
  }
  return -1
}

function findClosingBracket(value: string, start: number) {
  let depth = 0
  let index = start
  while (index < value.length) {
    if (value.startsWith('[Ann|', index) || value.startsWith('[Inside|', index)) {
      depth += 1
      index += value.startsWith('[Ann|', index) ? '[Ann|'.length : '[Inside|'.length
      continue
    }
    if (value[index] === ']') {
      if (depth === 0) return index
      depth -= 1
    }
    index += 1
  }
  return -1
}

function splitTopLevelPipes(value: string) {
  const parts: string[] = []
  let depth = 0
  let start = 0
  let index = 0
  while (index < value.length) {
    if (value.startsWith('[Ann|', index) || value.startsWith('[Inside|', index)) {
      depth += 1
      index += value.startsWith('[Ann|', index) ? '[Ann|'.length : '[Inside|'.length
      continue
    }
    if (value[index] === ']' && depth > 0) depth -= 1
    if (value[index] === '|' && depth === 0) {
      parts.push(value.slice(start, index))
      start = index + 1
    }
    index += 1
  }
  parts.push(value.slice(start))
  return parts
}

function readCodePoint(value: string, start: number) {
  const codePoint = value.codePointAt(start)
  const char = codePoint === undefined ? '' : String.fromCodePoint(codePoint)
  return { value: char, end: start + char.length }
}

function countDialogueVisibleCharacters(value: string) {
  return renderDialogueInlineMarkup(normalizeDialogueEscapes(value), Number.POSITIVE_INFINITY).consumed
}

function stripHtml(value: string) {
  return value.replace(/<[^>]*>/g, '')
}

function escapeHtmlAttribute(value: string) {
  return value
    .replace(/&/g, '&amp;')
    .replace(/"/g, '&quot;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
}

watch(
  () => props.visible,
  async (visible) => {
    if (visible) {
      await characterStore.load().catch((error) => console.warn('[PreviewPopup] 角色配置加载失败', error))
      await nextTick()
      restartPreview()
      window.addEventListener('keydown', handlePreviewKeydown)
      window.addEventListener('contextmenu', handlePreviewContextMenu)
    } else {
      window.removeEventListener('keydown', handlePreviewKeydown)
      window.removeEventListener('contextmenu', handlePreviewContextMenu)
      stopAudio()
    }
  }
)

watch(
  () => props.reloadToken,
  () => {
    if (props.visible) {
      restartPreview()
    }
  }
)

onBeforeUnmount(() => {
  window.removeEventListener('keydown', handlePreviewKeydown)
  window.removeEventListener('contextmenu', handlePreviewContextMenu)
  stopTypewriter()
  stopAudio()
  closeStageModal()
})

function openStageModal(width: number, height: number) {
  applyPreviewResolution(width, height)
  stageModalVisible.value = true
}

function startFullscreenRecordingPreview() {
  stageModalVisible.value = false
  requestPreviewStageFullscreen()
  restartPreview()
}

function requestPreviewStageFullscreen() {
  const target = stageRef.value
  if (!target || document.fullscreenElement) return
  target.requestFullscreen().catch((error) => {
    console.warn('[PreviewPopup] 全屏请求失败', error)
  })
}

function isObsBrowserSourceUserAgent() {
  return /\bOBS\/\d+(?:\.\d+)*\b/i.test(navigator.userAgent)
}

function handlePreviewContextMenu(event: MouseEvent) {
  if (!isObsBrowserSourceUserAgent()) return
  if (document.fullscreenElement !== stageRef.value) return

  event.preventDefault()
  document.exitFullscreen().catch((error) => {
    console.warn('[PreviewPopup] OBS 右键退出全屏失败', error)
  })
}

function closeStageModal() {
  stageModalVisible.value = false
}

function loadViewportSettings() {
  const settings = readSettings()
  const width = clampNumber(settings.previewWidth, DEFAULT_VIEWPORT.width, 640, 7680)
  const height = clampNumber(settings.previewHeight, DEFAULT_VIEWPORT.height, 360, 4320)
  viewport.value = { width, height }
}

function readSettings(): PreviewSettings {
  try {
    return JSON.parse(localStorage.getItem(SETTINGS_STORAGE_KEY) || '{}') as PreviewSettings
  } catch {
    return {}
  }
}

function persistViewportSettings(width: number, height: number) {
  const settings = readSettings()
  localStorage.setItem(SETTINGS_STORAGE_KEY, JSON.stringify({
    ...settings,
    previewWidth: width,
    previewHeight: height,
  }))
}

function handleResolutionChange(event: Event) {
  const target = event.target as HTMLSelectElement | null
  const resolution = PREVIEW_RESOLUTIONS.find((item) => item.key === target?.value)
  if (!resolution) return
  applyPreviewResolution(resolution.width, resolution.height)
}

function applyPreviewResolution(width: number, height: number) {
  viewport.value = { width, height }
  persistViewportSettings(width, height)
}

function clampNumber(value: unknown, fallback: number, min: number, max: number) {
  const numericValue = Number(value)
  if (!Number.isFinite(numericValue)) return fallback
  return Math.min(max, Math.max(min, Math.round(numericValue)))
}

function restartPreview() {
  loadViewportSettings()
  resetPlaybackState()

  const serialized = nodeGraphStore.serializeGraph()
  graphNodes.value = normalizePreviewNodes(serialized?.nodes || [])
  graphConnections.value = normalizePreviewConnections(serialized?.connections || [])

  console.groupCollapsed('[PreviewPopup] 预览执行图')
  console.log('nodes:', graphNodes.value.map((node) => ({ id: node.id, type: node.type, data: node.data, nextNodeUuids: node.nextNodeUuids })))
  console.log('connections:', graphConnections.value.map((connection) => `${connection.from.nodeId}:${connection.from.port} -> ${connection.to.nodeId}:${connection.to.port}`))
  console.groupEnd()

  if (graphNodes.value.length === 0) {
    statusMessage.value = '当前蓝图没有可预览的节点'
    console.warn('[PreviewPopup] 当前蓝图没有可预览的节点', serialized)
    return
  }

  const startNode = graphNodes.value.find((node) => node.type === 'StartNode') || graphNodes.value[0]
  currentNodeId.value = startNode.id

  runFromNode(startNode.id, 'restart')
}

function resetPlaybackState() {
  currentNodeId.value = null
  currentBackground.value = { url: '', type: 'image' }
  speaker.value = ''
  speakerAffiliation.value = ''
  dialogueText.value = ''
  fullDialogueText.value = ''
  visibleTextLength.value = 0
  dialogueVisible.value = false
  textComplete.value = false
  statusMessage.value = ''
  previewEnded.value = false
  choices.value = []
  characterSlots.value = {}
  resourceTraceByUrl.value = {}
  stopAudio()
}

function runFromNode(nodeId: string | null, reason = 'advance') {
  if (!nodeId) {
    pauseWithError('无法继续预览：下一个节点为空', { reason, currentNodeId: currentNodeId.value })
    return
  }

  const visited = new Set<string>()
  let nextNodeId: string | null = nodeId

  while (nextNodeId) {
    if (visited.has(nextNodeId)) {
      pauseWithError('检测到循环，预览已暂停', { nodeId: nextNodeId, visited: Array.from(visited) })
      return
    }

    visited.add(nextNodeId)
    const node = findNode(nextNodeId)
    if (!node) {
      pauseWithError(`找不到节点：${nextNodeId}`, { nodeId: nextNodeId, reason })
      return
    }

    currentNodeId.value = node.id
    console.debug('[PreviewPopup] 执行节点', { id: node.id, type: node.type, data: node.data })

    if (node.type === 'StartNode') {
      nextNodeId = getNextNodeId(node.id)
      if (!nextNodeId) {
        pauseWithError('StartNode 没有连接到下一个执行节点', { node, connections: graphConnections.value })
        return
      }
      continue
    }

    if (node.type === 'EventNode') {
      processEventNode(node)
      nextNodeId = getNextNodeId(node.id)
      continue
    }

    if (node.type === 'EndNode') {
      processEndNode(node)
      return
    }

    if (node.type === 'DialogueNode') {
      renderDialogueNode(node)
      return
    }

    if (node.type === 'BranchNode') {
      renderBranchNode(node)
      return
    }

    if (node.type === 'ChoiceNode') {
      renderChoiceNode(node)
      return
    }

    nextNodeId = getNextNodeId(node.id)
    if (!nextNodeId) {
      pauseWithError(`节点没有下一个执行连接：${node.type} (${node.id})`, { node, connections: graphConnections.value })
      return
    }
  }
}

function renderDialogueNode(node: PreviewNode) {
  applyCharacterControls(node)
  playDialogueVoices(node)
  choices.value = []
  statusMessage.value = ''
  const speakerId = getDialogueSpeaker(node)
  const speakerInfo = getCharacterDisplayInfo(speakerId)
  speaker.value = speakerInfo.name
  speakerAffiliation.value = speakerInfo.affiliation
  startTypewriter(String(node.data.text || ''))
  dialogueVisible.value = true
}

function startTypewriter(text: string) {
  stopTypewriter()
  fullDialogueText.value = text
  dialogueText.value = ''
  visibleTextLength.value = 0
  const totalVisibleLength = countDialogueVisibleCharacters(text)
  textComplete.value = totalVisibleLength === 0

  if (totalVisibleLength === 0) return

  const step = () => {
    visibleTextLength.value += 1
    dialogueText.value = stripHtml(renderDialogueInlineMarkup(normalizeDialogueEscapes(text), visibleTextLength.value).html)
    if (visibleTextLength.value >= totalVisibleLength) {
      textComplete.value = true
      typewriterTimer = null
      return
    }
    typewriterTimer = window.setTimeout(step, getTypewriterDelay(dialogueText.value.slice(-1)))
  }

  typewriterTimer = window.setTimeout(step, getTypewriterDelay(''))
}

function stopTypewriter() {
  if (typewriterTimer !== null) {
    window.clearTimeout(typewriterTimer)
    typewriterTimer = null
  }
}

function completeTypewriter() {
  stopTypewriter()
  dialogueText.value = fullDialogueText.value
  visibleTextLength.value = countDialogueVisibleCharacters(fullDialogueText.value)
  textComplete.value = true
}

function getTypewriterDelay(character: string) {
  return /[，。！？、,.!?]/.test(character) ? 86 : 28
}

function getDialogueSpeaker(node: PreviewNode) {
  const speakerSlot = String(node.data.speakerSlot ?? '').trim()
  const controls = getCharacterControlsForDialogue(node)
  if (speakerSlot === 'all') {
    return visibleCharacters.value.map(character => character.character).filter(Boolean).join(' / ')
  }
  if (speakerSlot) {
    const controlledSpeakerSlot = controls.find((control: any) => String(control?.slot || '') === speakerSlot)
    if (controlledSpeakerSlot?.character || controlledSpeakerSlot?.unmanagedCharacter) {
      return String(speakerSlot === '6'
        ? controlledSpeakerSlot.unmanagedCharacter || controlledSpeakerSlot.character || ''
        : controlledSpeakerSlot.character || '').trim()
    }
    const slot6OnlyControl = controls.length > 0
      && !controls.some((control: any) => String(control?.slot || '') !== '6')
      && controls.find((control: any) => String(control?.slot || '') === '6')
    if (speakerSlot === '1' && slot6OnlyControl) {
      return String((slot6OnlyControl as any).unmanagedCharacter || (slot6OnlyControl as any).character || '').trim()
    }
    return String(characterSlots.value[speakerSlot]?.character || '').trim()
  }

  const directSpeaker = String(node.data.speaker || node.data.character || '').trim()
  if (directSpeaker) return directSpeaker

  const firstSpeakerControl = controls.find((control: any) => control?.character || control?.unmanagedCharacter)
  if (!firstSpeakerControl) return ''
  return String(firstSpeakerControl.slot === '6'
    ? firstSpeakerControl.unmanagedCharacter || firstSpeakerControl.character || ''
    : firstSpeakerControl.character || '').trim()
}

function renderBranchNode(node: PreviewNode) {
  dialogueVisible.value = false
  statusMessage.value = String(node.data.condition || '请选择分支')
  choices.value = [
    { text: '是', port: 'execTrue' },
    { text: '否', port: 'execFalse' },
  ].filter((choice) => Boolean(getNextNodeId(node.id, choice.port)))
}

function renderChoiceNode(node: PreviewNode) {
  dialogueVisible.value = false
  statusMessage.value = '请选择选项'
  choices.value = Object.entries(node.data)
    .filter(([key]) => key.startsWith('choiceText_'))
    .map(([key, value]) => ({
      text: String(value || `选项 ${key.replace('choiceText_', '')}`),
      port: `execOut_${key.replace('choiceText_', '')}`,
    }))
    .filter((choice) => Boolean(getNextNodeId(node.id, choice.port)))
}

function processEventNode(node: PreviewNode) {
  const subType = inferEventSubType(node.data)
  const resourcePath = getEventResourcePath(node.data, subType)

  if (subType === 'changeBackground') {
    setBackground(resourcePath.value, createResourceTrace(node, 'background', '背景', resourcePath.field, resourcePath.value, resolveAssetUrl(resourcePath.value, 'Backgrounds')))
  } else if (subType === 'playBgm') {
    playBgm(resourcePath.value, Number(node.data.volume ?? 1), createResourceTrace(node, 'bgm', 'BGM', resourcePath.field, resourcePath.value, resolveAssetUrl(resourcePath.value, 'Musics')))
  } else if (subType === 'stopBgm') {
    stopAudio()
  } else if (subType === 'playSfx') {
    playSfx(resourcePath.value, Number(node.data.volume ?? 1), createResourceTrace(node, 'sfx', '音效', resourcePath.field, resourcePath.value, resolveAssetUrl(resourcePath.value, 'Sfx')))
  } else if (subType === 'playVoice') {
    playVoice(resourcePath.value, createResourceTrace(node, 'voice', '语音', resourcePath.field, resourcePath.value, resolveAssetUrl(resourcePath.value, 'Voices')))
  } else if (subType === 'showCharacter') {
    setCharacterSlot('1', {
      character: String(node.data.characterId || ''),
      sprite: resourcePath.value || String(node.data.characterId || ''),
      position: String(node.data.position || 'center'),
      animation: 'fade',
      easing: 'easeOutCubic',
      duration: 0.3,
    }, node)
  } else if (subType === 'hideCharacter') {
    const characterId = String(node.data.characterId || '')
    characterSlots.value = Object.fromEntries(Object.entries(characterSlots.value).filter(([, slot]) => slot.character !== characterId))
  } else {
    console.warn('[PreviewPopup] 无法识别 EventNode 类型，已跳过', { node, inferredSubType: subType })
  }
}

function inferEventSubType(data: Record<string, any>) {
  const explicitType = normalizeEventSubType(data.subType || data.eventType || data.eventName)
  if (explicitType) return explicitType

  if (hasAnyResourceField(data, ['imagePath', 'background', 'bgFile', 'bg'])) return 'changeBackground'
  if (hasAnyResourceField(data, ['bgmPath', 'bgmFile', 'bgm', 'musicFile'])) return 'playBgm'
  if (hasAnyResourceField(data, ['sfxPath', 'soundFile', 'sfx'])) return 'playSfx'
  if (hasAnyResourceField(data, ['voicePath', 'voiceFile', 'voice'])) return 'playVoice'
  if (hasAnyResourceField(data, ['sprite', 'spritePath', 'characterSprite']) || data.characterId) return 'showCharacter'

  return ''
}

function hasAnyResourceField(data: Record<string, any>, fields: string[]) {
  return fields.some((field) => String(data[field] || '').trim())
}

function getEventResourcePath(data: Record<string, any>, subType: string) {
  if (subType === 'changeBackground') {
    return pickResourceField(data, ['imagePath', 'background', 'bgFile', 'bg', 'resource', 'path', 'file', 'filePath'])
  }

  if (subType === 'playBgm') {
    return pickResourceField(data, ['bgmPath', 'bgmFile', 'bgm', 'musicFile', 'resource', 'path', 'file', 'filePath'])
  }

  if (subType === 'playSfx') {
    return pickResourceField(data, ['sfxPath', 'soundFile', 'sfx', 'resource', 'path', 'file', 'filePath'])
  }

  if (subType === 'playVoice') {
    return pickResourceField(data, ['voicePath', 'voiceFile', 'voice', 'resource', 'path', 'file', 'filePath'])
  }

  if (subType === 'showCharacter') {
    return pickResourceField(data, ['sprite', 'spritePath', 'characterSprite', 'resource', 'path', 'file', 'filePath'])
  }

  return { field: '', value: '' }
}

function pickResourceField(data: Record<string, any>, fields: string[]) {
  for (const field of fields) {
    const value = String(data[field] || '').trim()
    if (value) return { field, value }
  }

  return { field: fields[0] || '', value: '' }
}

function normalizeEventSubType(value: unknown) {
  const eventType = String(value || '').trim()
  const aliases: Record<string, string> = {
    ChangeBGM: 'playBgm',
    ChangeBgm: 'playBgm',
    changeBgm: 'playBgm',
    PlayBGM: 'playBgm',
    StopBGM: 'stopBgm',
    StopBgm: 'stopBgm',
    PlaySFX: 'playSfx',
    PlayVoice: 'playVoice',
    ChangeBackground: 'changeBackground',
    ChangeBG: 'changeBackground',
    changeBG: 'changeBackground',
    changeBg: 'changeBackground',
    ShowCharacter: 'showCharacter',
    HideCharacter: 'hideCharacter',
  }

  return aliases[eventType] || eventType
}

function processEndNode(node: PreviewNode) {
  if (node.data.eventType === 'jump_to_scene' && node.data.sceneId) {
    statusMessage.value = `跳转到场景：${node.data.sceneId}`
  } else if (node.data.eventType && node.data.eventType !== 'end_game') {
    statusMessage.value = `执行结束事件：${node.data.eventType}`
    console.info('[PreviewPopup] 执行 EndNode 事件', node.data)
  } else {
    showEnd()
  }
}

function applyCharacterControls(dialogueNode: PreviewNode) {
  const controls = getCharacterControlsForDialogue(dialogueNode)
  const nextSlots = { ...characterSlots.value }

  for (const control of controls) {
    const slot = String(control.slot || '1')
    const action = String(control.action || 'show').toLowerCase()

    if (control.sfx) {
      playSfx(String(control.sfx), 0.9)
    }

    if (action === 'hide') {
      delete nextSlots[slot]
      continue
    }

    const sprite = slot === '6' ? '' : String(control.sprite || nextSlots[slot]?.sprite || '')
    const spriteUrl = sprite ? resolveAssetUrl(sprite, 'Characters') : ''
    const character = slot === '6'
      ? String(control.unmanagedCharacter || control.character || nextSlots[slot]?.character || '')
      : String(control.character || nextSlots[slot]?.character || '')
    if (sprite) {
      registerResourceTrace(createResourceTrace(dialogueNode, 'character', `角色立绘 slot ${slot}`, 'characterControls.sprite', sprite, spriteUrl))
    }
    const fromPosition = String(control.fromPosition || '').trim()
    const requestedTargetPosition = String(control.toPosition || '').trim()
    const targetPosition = requestedTargetPosition && requestedTargetPosition !== 'none'
      ? requestedTargetPosition
      : String(control.position || nextSlots[slot]?.position || 'center')
    const animation = String(control.animation || (action === 'move' ? 'move' : 'fade'))
    const shouldMove = (action === 'move' || animation === 'move') && requestedTargetPosition !== 'none'

    nextSlots[slot] = {
      slot,
      character,
      sprite,
      spriteUrl,
      position: targetPosition,
      animation,
      fromPosition: shouldMove ? (fromPosition || nextSlots[slot]?.position || targetPosition) : '',
      easing: String(control.easing || 'easeOutCubic'),
      duration: Number(control.duration ?? 0.3),
    }
  }

  characterSlots.value = nextSlots
}

function getCharacterControlsForDialogue(dialogueNode: PreviewNode) {
  const inlineControls = Array.isArray(dialogueNode.data.characterControls) ? dialogueNode.data.characterControls : []
  const linkedControls = graphConnections.value
    .filter((connection) => connection.to.nodeId === dialogueNode.id && normalizePort(connection.to.port).startsWith('characterControl'))
    .map((connection) => {
      const controlNode = findNode(connection.from.nodeId)
      if (!controlNode || controlNode.type !== 'CharacterControlNode') return null
      const slotFromPort = normalizePort(connection.to.port).replace('characterControl', '')
      return {
        ...controlNode.data,
        slot: controlNode.data.slot || slotFromPort || '1',
      }
    })
    .filter(Boolean)

  return [...inlineControls, ...linkedControls]
}

function playDialogueVoices(node: PreviewNode) {
  const voiceEntries = collectDialogueVoices(node)
  for (const voice of voiceEntries) {
    playVoice(voice.path, createResourceTrace(node, 'voice', `语音 slot ${voice.slot}`, voice.field, voice.path, resolveAssetUrl(voice.path, 'Voices')))
  }
}

function collectDialogueVoices(node: PreviewNode) {
  const entries: Array<{ slot: string; path: string; field: string }> = []
  for (let slot = 1; slot <= 6; slot += 1) {
    const field = `voice${slot}`
    const path = String(node.data[field] || '').trim()
    if (path) entries.push({ slot: String(slot), path, field })
  }

  if (Array.isArray(node.data.voices)) {
    node.data.voices.forEach((voice: any, index: number) => {
      const path = String(voice?.path || voice?.voice || voice || '').trim()
      if (path) entries.push({ slot: String(voice?.slot || index + 1), path, field: 'voices' })
    })
  }

  const legacyVoice = String(node.data.voice || '').trim()
  if (legacyVoice && !entries.some(entry => entry.path === legacyVoice)) {
    entries.push({ slot: String(node.data.speakerSlot || '1'), path: legacyVoice, field: 'voice' })
  }

  return entries
}

function setCharacterSlot(slot: string, data: Omit<CharacterSlotState, 'slot' | 'spriteUrl'>, sourceNode?: PreviewNode) {
  const spriteUrl = resolveAssetUrl(data.sprite, 'Characters')
  if (sourceNode) {
    registerResourceTrace(createResourceTrace(sourceNode, 'character', `角色立绘 slot ${slot}`, 'sprite', data.sprite, spriteUrl))
  }

  characterSlots.value = {
    ...characterSlots.value,
    [slot]: {
      slot,
      ...data,
      spriteUrl,
    },
  }
}

function getCharacterStyle(character: CharacterSlotState) {
  const duration = Math.max(0, Number(character.duration ?? 0.3))
  const easing = EASING_CURVES[character.easing || 'easeOutCubic'] || EASING_CURVES.easeOutCubic
  const target = character.position || 'center'
  const from = character.fromPosition || ''
  const moving = character.animation === 'move' && from && from !== target

  return {
    '--character-left': POSITION_LEFT[target] || POSITION_LEFT.center,
    '--character-transform': POSITION_TRANSFORM[target] || POSITION_TRANSFORM.center,
    '--character-from-left': POSITION_LEFT[from] || POSITION_LEFT[target] || POSITION_LEFT.center,
    '--character-from-transform': POSITION_TRANSFORM[from] || POSITION_TRANSFORM[target] || POSITION_TRANSFORM.center,
    '--character-duration': `${duration}s`,
    '--character-easing': easing,
    left: POSITION_LEFT[target] || POSITION_LEFT.center,
    right: 'auto',
    transform: POSITION_TRANSFORM[target] || POSITION_TRANSFORM.center,
    animation: moving ? `preview-character-move ${duration}s ${easing}` : undefined,
  }
}

function getCharacterDisplayInfo(characterId: string) {
  const id = String(characterId || '').trim()
  if (!id) return { name: '', affiliation: '' }

  const character = characterStore.characters.find(item => item.id === id || item.name === id || item.displayId === id)
  return {
    name: character?.displayId || character?.name || id,
    affiliation: character?.affiliation || '',
  }
}

function advanceFromStage() {
  if (previewEnded.value) return
  if (choices.value.length > 0) return
  if (dialogueVisible.value) advancePreview()
}

function handlePreviewKeydown(event: KeyboardEvent) {
  if (!props.visible) return
  if (event.key !== ' ' && event.key !== 'Spacebar' && event.key !== 'Enter') return

  const target = event.target as HTMLElement | null
  const tagName = target?.tagName?.toLowerCase()
  if (tagName === 'input' || tagName === 'textarea' || tagName === 'select' || target?.isContentEditable) return

  event.preventDefault()
  advanceFromStage()
}

function advancePreview() {
  if (previewEnded.value) return
  if (!textComplete.value) {
    completeTypewriter()
    return
  }
  const nextNodeId = getNextNodeId(currentNodeId.value)
  if (!nextNodeId) {
    pauseWithError('当前对白节点没有下一个执行连接，预览已暂停', { currentNodeId: currentNodeId.value, connections: graphConnections.value })
    return
  }
  dialogueVisible.value = false
  stopTypewriter()
  runFromNode(nextNodeId, 'dialogue-click')
}

function selectChoice(port: string) {
  if (previewEnded.value) return
  const nextNodeId = getNextNodeId(currentNodeId.value, port)
  if (!nextNodeId) {
    pauseWithError(`选项端口没有连接：${port}`, { currentNodeId: currentNodeId.value, port, connections: graphConnections.value })
    return
  }
  choices.value = []
  statusMessage.value = ''
  runFromNode(nextNodeId, `choice:${port}`)
}

function getNextNodeId(nodeId: string | null, sourcePort?: string): string | null {
  if (!nodeId) return null
  const connection = graphConnections.value.find((candidate) => {
    if (candidate.from.nodeId !== nodeId) return false
    return sourcePort ? portsEqual(candidate.from.port, sourcePort) : isExecOutPort(candidate.from.port)
  })
  if (connection?.to.nodeId) return connection.to.nodeId

  const node = findNode(nodeId)
  const fallbackNext = !sourcePort ? node?.nextNodeUuids?.[0] : undefined
  if (fallbackNext) {
    console.warn('[PreviewPopup] 未找到执行连接，使用 nextNodeUuids 兜底', { nodeId, fallbackNext, node })
    return fallbackNext
  }

  console.warn('[PreviewPopup] 未找到下一个节点', { nodeId, sourcePort, outgoing: graphConnections.value.filter((candidate) => candidate.from.nodeId === nodeId) })
  return null
}

function isExecOutPort(port: string) {
  const normalizedPort = normalizePort(port)
  return normalizedPort === 'execOut' || normalizedPort.startsWith('execOut_') || normalizedPort === 'execTrue' || normalizedPort === 'execFalse'
}

function findNode(nodeId: string) {
  return graphNodes.value.find((node) => node.id === nodeId)
}

function normalizePreviewNodes(nodes: GraphNode[]): PreviewNode[] {
  return nodes.map((node: any) => {
    const id = String(node.id || node.uuid || '')
    const type = normalizeNodeType(node.type || node.nodeType)
    const data = {
      ...(node.properties || {}),
      ...(node.data || {}),
    }

    if (node.subType && !data.subType) {
      data.subType = node.subType
    }

    if (data.event?.eventType && !data.subType) {
      data.subType = data.event.eventType
    }

    if (data.event?.parameters) {
      Object.assign(data, normalizeEventParameters(data.event.parameters))
    }

    return {
      id,
      type,
      data,
      nextNodeUuids: Array.isArray(node.nextNodeUuids) ? node.nextNodeUuids.map(String) : [],
    }
  }).filter((node) => node.id)
}

function normalizeEventParameters(parameters: Record<string, any>) {
  const resource = parameters.resource || parameters.path || parameters.file || parameters.filePath
  const background = parameters.background || parameters.bgFile || parameters.bg || parameters.imagePath || resource
  const bgmFile = parameters.bgmFile || parameters.bgm || parameters.bgmPath || parameters.musicFile || resource

  return {
    resource,
    background,
    imagePath: background,
    bgmFile,
    bgmPath: bgmFile,
    sfxPath: parameters.soundFile || parameters.sfx || resource,
    voicePath: parameters.voiceFile || parameters.voice || resource,
  }
}

function normalizePreviewConnections(connections: GraphConnection[]): PreviewConnection[] {
  return connections.map((connection: any) => {
    const fromNodeId = String(connection.from?.nodeId || connection.sourceNodeUuid || connection.source || '')
    const toNodeId = String(connection.to?.nodeId || connection.targetNodeUuid || connection.target || '')
    const fromPort = normalizePort(connection.from?.port || connection.sourcePort)
    const toPort = normalizePort(connection.to?.port || connection.targetPort)
    const id = String(connection.id || connection.uuid || `${fromNodeId}:${fromPort}->${toNodeId}:${toPort}`)

    return {
      id,
      from: { nodeId: fromNodeId, port: fromPort },
      to: { nodeId: toNodeId, port: toPort },
    }
  }).filter((connection) => connection.from.nodeId && connection.to.nodeId)
}

function normalizeNodeType(type: unknown) {
  const nodeType = String(type || 'UnknownNode')
  const aliases: Record<string, string> = {
    Start: 'StartNode',
    End: 'EndNode',
    Event: 'EventNode',
    Dialogue: 'DialogueNode',
    Branch: 'BranchNode',
    Choice: 'ChoiceNode',
    CharacterControl: 'CharacterControlNode',
  }

  return aliases[nodeType] || nodeType
}

function normalizePort(port: unknown) {
  const name = String(port || '').trim()
  if (name === 'exec_out') return 'execOut'
  if (name === 'exec_in') return 'execIn'
  if (name === '→') return 'execOut'
  return name
}

function portsEqual(left: string, right: string) {
  return normalizePort(left) === normalizePort(right)
}

function pauseWithError(message: string, detail?: unknown) {
  choices.value = []
  dialogueVisible.value = false
  statusMessage.value = message
  previewEnded.value = false
  console.error(`[PreviewPopup] ${message}`, detail)
}

function setBackground(path: string, trace?: PreviewResourceTrace) {
  const url = trace?.resolvedUrl || resolveAssetUrl(path, 'Backgrounds')
  currentBackground.value = {
    url,
    type: isVideoAsset(path) ? 'video' : 'image',
  }
  registerResourceTrace(trace)
  if (path) console.debug('[PreviewPopup] 设置背景资源', { path, url })
}

function isVideoAsset(path: string) {
  const extension = path.split(/[?#]/)[0].toLowerCase().match(/\.[^.\/\\]+$/)?.[0] || ''
  return VIDEO_ASSET_EXTENSIONS.has(extension)
}

function playBgm(path: string, volume: number, trace?: PreviewResourceTrace) {
  if (!bgmAudio.value || !path) return
  const url = trace?.resolvedUrl || resolveAssetUrl(path, 'Musics')
  registerResourceTrace(trace)
  bgmAudio.value.src = url
  bgmAudio.value.volume = Math.max(0, Math.min(1, volume))
  bgmAudio.value.play().catch((error) => {
    console.warn('[PreviewPopup] BGM 播放失败:', { path, url, error })
  })
}

async function logMediaError(type: string, url: string) {
  const trace = resourceTraceByUrl.value[url]
  const backend = await readPreviewErrorResponse(url)
  const message = `${type}加载失败：${trace?.rawPath || url}`
  statusMessage.value = trace
    ? `${message}\n节点 ${trace.nodeType} (${trace.nodeId}) · 字段 ${trace.field}\nURL: ${url}${backend ? `\n后端: ${backend}` : ''}`
    : `${message}${backend ? `\n后端: ${backend}` : ''}`

  console.warn(`[PreviewPopup] ${type}加载失败`, { url, trace, backend })
}

function playSfx(path: string, volume: number, trace?: PreviewResourceTrace) {
  if (!path) return
  const audio = new Audio(trace?.resolvedUrl || resolveAssetUrl(path, 'Sfx'))
  registerResourceTrace(trace)
  audio.volume = Math.max(0, Math.min(1, volume))
  audio.play().catch((error) => {
    reportAudioPlaybackError('音效', path, audio.src, error)
  })
}

function playVoice(path: string, trace?: PreviewResourceTrace) {
  if (!path) return
  const audio = new Audio(trace?.resolvedUrl || resolveAssetUrl(path, 'Voices'))
  registerResourceTrace(trace)
  audio.volume = 1
  audio.play().catch((error) => {
    reportAudioPlaybackError('语音', path, audio.src, error)
  })
}

function createResourceTrace(node: PreviewNode, kind: PreviewResourceKind, label: string, field: string, rawPath: string, resolvedUrl: string): PreviewResourceTrace {
  return {
    kind,
    label,
    nodeId: node.id,
    nodeType: node.type,
    field,
    rawPath,
    resolvedUrl,
  }
}

function registerResourceTrace(trace?: PreviewResourceTrace) {
  if (!trace?.resolvedUrl) return
  resourceTraceByUrl.value = {
    ...resourceTraceByUrl.value,
    [trace.resolvedUrl]: trace,
  }
}

async function readPreviewErrorResponse(url: string) {
  if (!url || url.startsWith('data:') || url.startsWith('blob:')) return ''

  try {
    const response = await fetch(url, { method: 'GET', cache: 'no-store' })
    if (response.ok) return ''

    const contentType = response.headers.get('content-type') || ''
    if (contentType.includes('application/json')) {
      const body = await response.json().catch(() => null)
      return body?.error || body?.message || `${response.status} ${response.statusText}`
    }

    return `${response.status} ${response.statusText}`
  } catch (error) {
    return error instanceof Error ? error.message : String(error)
  }
}

function reportAudioPlaybackError(type: string, path: string, url: string, error: unknown) {
  const trace = resourceTraceByUrl.value[url]
  statusMessage.value = `${type}播放失败：${path}\n${trace ? `节点 ${trace.nodeType} (${trace.nodeId}) · 字段 ${trace.field}\n` : ''}URL: ${url}`
  console.warn(`[PreviewPopup] ${type}播放失败`, { path, url, trace, error })
}

function stopAudio() {
  if (!bgmAudio.value) return
  bgmAudio.value.pause()
  bgmAudio.value.removeAttribute('src')
  bgmAudio.value.load()
}

function showEnd() {
  choices.value = []
  previewEnded.value = true
  currentNodeId.value = null
  speaker.value = ''
  speakerAffiliation.value = ''
  fullDialogueText.value = '— 预览结束 —'
  dialogueText.value = '— 预览结束 —'
  textComplete.value = true
  dialogueVisible.value = true
  statusMessage.value = ''
}

function closePreview() {
  emit('close')
}
</script>

<style scoped>
.preview-popup-overlay {
  position: fixed;
  inset: 0;
  z-index: 2147483647 !important;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 24px;
  background: rgba(0, 0, 0, 0.72);
  box-sizing: border-box;
  isolation: isolate !important;
  color-scheme: dark !important;
  font-family: Gadugi, "Segoe UI", sans-serif !important;
}

.preview-popup-overlay,
.preview-popup-overlay * {
  box-sizing: border-box !important;
  font-synthesis: none !important;
  text-rendering: geometricPrecision !important;
}

.preview-popup-dialog {
  width: min(100%, calc(100vw - 48px));
  max-height: calc(100vh - 48px);
  display: flex;
  flex-direction: column;
  overflow: hidden;
  background: #1e1e1e;
  border: 1px solid #3e3e42;
  border-radius: 10px;
  box-shadow: 0 24px 80px rgba(0, 0, 0, 0.45);
  color: #ffffff !important;
  forced-color-adjust: none !important;
  isolation: isolate !important;
}

.preview-popup-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding: 12px 14px;
  background: #252526;
  border-bottom: 1px solid #3e3e42;
}

.preview-popup-header h3 {
  margin: 0;
  color: #ffffff;
  font-size: 15px;
}

.preview-popup-header p {
  margin: 4px 0 0;
  color: #9ca3af;
  font-size: 12px;
}

.preview-popup-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

.preview-resolution-picker {
  display: flex;
  align-items: center;
  gap: 8px;
  color: #cbd5e1;
  font-size: 12px;
  white-space: nowrap;
}

.preview-resolution-picker select {
  min-width: 132px;
  border: 1px solid #3e3e42;
  border-radius: 6px;
  padding: 6px 28px 6px 10px;
  background: #333337;
  color: #ffffff;
  cursor: pointer;
}

.preview-popup-window-actions {
  display: flex;
  align-items: center;
  gap: 6px;
  color: #cbd5e1;
  font-size: 12px;
  white-space: nowrap;
}

.preview-popup-actions button {
  border: 1px solid #3e3e42;
  border-radius: 6px;
  padding: 6px 10px;
  background: #333337;
  color: #cccccc;
  cursor: pointer;
}

.preview-popup-actions button:hover {
  background: #0e639c;
  color: #ffffff;
}

.preview-popup-close {
  width: 30px;
  height: 30px;
  padding: 0 !important;
}

.preview-popup-body {
  min-height: 0;
  padding: 16px;
  overflow: auto;
  background: #111827;
}

.preview-stage-shell {
  width: 100%;
  margin: 0 auto;
  background: #000000 !important;
  overflow: hidden !important;
  forced-color-adjust: none !important;
  isolation: isolate !important;
}

.preview-stage {
  position: relative;
  transform-origin: top left;
  scale: min(calc((100vw - 80px) / var(--preview-width, 1280)), calc((100vh - 150px) / var(--preview-height, 720)), 1);
  overflow: hidden !important;
  background: #050505 !important;
  color: #ffffff !important;
  cursor: pointer !important;
  font-family: Gadugi, "Segoe UI", sans-serif !important;
  forced-color-adjust: none !important;
  isolation: isolate !important;
  contain: layout paint style !important;
}

.preview-stage:fullscreen {
  width: 100vw !important;
  height: 100vh !important;
  scale: 1 !important;
  transform: none !important;
  background: #000000 !important;
}

.preview-stage:fullscreen .preview-continue-indicator {
  display: none !important;
}

.preview-recording-modal {
  position: fixed;
  inset: 0;
  z-index: 2147483647;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(0, 0, 0, 0.82);
  color: #ffffff;
  font-family: Gadugi, "Segoe UI", sans-serif;
}

.preview-recording-launcher {
  width: min(420px, calc(100vw - 40px));
  padding: 28px;
  text-align: center;
  background: rgba(17, 24, 39, 0.96);
  border: 1px solid rgba(148, 163, 184, 0.32);
  border-radius: 18px;
  box-shadow: 0 24px 80px rgba(0, 0, 0, 0.5);
}

.preview-recording-launcher h3 {
  margin: 0 0 8px;
  font-size: 20px;
}

.preview-recording-launcher p {
  margin: 0 0 22px;
  color: #94a3b8;
  font-size: 13px;
}

.preview-recording-play,
.preview-recording-cancel {
  width: 100%;
  border: 0;
  border-radius: 10px;
  padding: 12px 16px;
  cursor: pointer;
  font-size: 15px;
}

.preview-recording-play {
  background: #2563eb;
  color: #ffffff;
  font-weight: 700;
}

.preview-recording-play:hover {
  background: #1d4ed8;
}

.preview-recording-cancel {
  margin-top: 10px;
  background: #334155;
  color: #cbd5e1;
}

.preview-recording-cancel:hover {
  background: #475569;
}

.preview-stage,
.preview-stage * {
  scrollbar-color: auto !important;
  caret-color: auto !important;
  accent-color: auto !important;
}

.preview-stage button,
.preview-stage input,
.preview-stage textarea,
.preview-stage select {
  font: inherit !important;
}

.preview-stage,
.preview-stage * {
  user-select: none !important;
  -webkit-user-select: none !important;
}

.preview-background,
.preview-background-placeholder {
  position: absolute !important;
  inset: 0 !important;
  width: 100% !important;
  height: 100% !important;
  object-fit: cover !important;
  opacity: 1 !important;
  mix-blend-mode: normal !important;
  filter: none !important;
}

.preview-background-placeholder {
  display: flex;
  align-items: center;
  justify-content: center;
  background: radial-gradient(circle at center, #263545 0%, #0f172a 62%, #020617 100%);
  color: rgba(255, 255, 255, 0.22);
  font-size: 52px;
  letter-spacing: 0.08em;
}

.preview-characters {
  position: absolute;
  inset: 0;
  pointer-events: none;
}

.preview-character {
  position: absolute !important;
  bottom: -18% !important;
  left: var(--character-left, 50%) !important;
  max-height: 112% !important;
  max-width: 52% !important;
  object-fit: contain !important;
  opacity: 1 !important;
  mix-blend-mode: normal !important;
  filter: drop-shadow(0 18px 28px rgba(0, 0, 0, 0.45)) !important;
  transform: var(--character-transform, translateX(-50%)) !important;
  transition: left var(--character-duration, 0.3s) var(--character-easing, cubic-bezier(0.22, 1, 0.36, 1)), transform var(--character-duration, 0.3s) var(--character-easing, cubic-bezier(0.22, 1, 0.36, 1)) !important;
}

.preview-character-left {
  left: 10% !important;
  transform: none !important;
}

.preview-character-center {
  left: 50% !important;
  transform: translateX(-50%) !important;
}

.preview-character-right {
  left: 60% !important;
  right: auto !important;
  transform: none !important;
}

.preview-character-move {
  animation: preview-character-move var(--character-duration, 0.3s) var(--character-easing, cubic-bezier(0.22, 1, 0.36, 1));
}

@keyframes preview-character-move {
  from {
    left: var(--character-from-left, 50%);
    transform: var(--character-from-transform, translateX(-50%));
  }
  to {
    left: var(--character-left, 50%);
    transform: var(--character-transform, translateX(-50%));
  }
}

.preview-character-fade,
.preview-character-pop {
  animation: preview-character-in 180ms ease-out;
}

.preview-dialogue {
  position: absolute !important;
  inset: 0 !important;
  padding: 0 !important;
  box-sizing: border-box !important;
  background: linear-gradient(180deg, rgba(7, 10, 18, 0) 0%, rgba(7, 10, 18, 0.16) 48%, rgba(8, 12, 20, 0.72) 77%, rgba(8, 12, 20, 0.9) 100%) !important;
  pointer-events: auto !important;
  color: #ffffff !important;
  opacity: 1 !important;
  mix-blend-mode: normal !important;
  filter: none !important;
}

.preview-speaker-row {
  position: absolute !important;
  left: 9.74% !important;
  top: 72.13% !important;
  display: flex !important;
  align-items: baseline !important;
  gap: clamp(28px, 1.8vw, 44px) !important;
  white-space: nowrap !important;
}

.preview-speaker {
  color: #ffffff !important;
  font-family: "Franklin Gothic Demi", "Franklin Gothic Medium", Gadugi, "Segoe UI", sans-serif !important;
  font-size: calc(var(--preview-width, 1920) * 67.54px / 1920) !important;
  font-weight: 700 !important;
  letter-spacing: 0.01em !important;
  line-height: 1 !important;
  -webkit-text-stroke: calc(var(--preview-width, 1920) * 2px / 1920) #00204c !important;
  paint-order: stroke fill !important;
  text-shadow: 0 3px 8px rgba(0, 32, 76, 0.42) !important;
}

.preview-affiliation {
  color: #7accf9 !important;
  font-family: "Franklin Gothic Demi", "Franklin Gothic Medium", Gadugi, "Segoe UI", sans-serif !important;
  font-size: calc(var(--preview-width, 1920) * 47.36px / 1920) !important;
  font-weight: 700 !important;
  line-height: 1 !important;
  -webkit-text-stroke: calc(var(--preview-width, 1920) * 2px / 1920) #00204c !important;
  paint-order: stroke fill !important;
  text-shadow: 0 3px 8px rgba(0, 32, 76, 0.38) !important;
}

.preview-dialogue-line {
  position: absolute !important;
  left: 9.38% !important;
  right: 8.8% !important;
  top: 79.44% !important;
  height: 2px !important;
  background: #ffffff !important;
  opacity: 1 !important;
}

.preview-text {
  position: absolute !important;
  left: 9.79% !important;
  right: 9.2% !important;
  top: 82.2% !important;
  white-space: pre-wrap !important;
  color: rgba(255, 255, 255, 0.94) !important;
  font-family: Gadugi, "Segoe UI", sans-serif !important;
  font-size: calc(var(--preview-width, 1920) * 46.9px / 1920) !important;
  font-weight: 400 !important;
  line-height: 1.32 !important;
  text-shadow: 0 3px 8px rgba(0, 0, 0, 0.5) !important;
}

.preview-text :deep(p) {
  margin: 0 !important;
  color: inherit !important;
  font: inherit !important;
  line-height: inherit !important;
}

.preview-text :deep(*) {
  color: inherit !important;
  line-height: inherit !important;
}

.preview-text :deep(p + p) {
  margin-top: 0.35em !important;
}

.preview-text :deep(h1),
.preview-text :deep(h2),
.preview-text :deep(h3),
.preview-text :deep(h4),
.preview-text :deep(h5),
.preview-text :deep(h6) {
  margin: 0 0 0.16em !important;
  color: #ffffff !important;
  font-family: inherit !important;
  font-weight: 700 !important;
  line-height: 1.08 !important;
}

.preview-text :deep(h1) { font-size: 1.28em !important; }
.preview-text :deep(h2) { font-size: 1.18em !important; }
.preview-text :deep(h3),
.preview-text :deep(h4),
.preview-text :deep(h5),
.preview-text :deep(h6) { font-size: 1.08em !important; }

.preview-text :deep(strong) {
  color: inherit !important;
  font-family: inherit !important;
  font-size: inherit !important;
  font-weight: 700 !important;
  line-height: inherit !important;
}

.preview-text :deep(em) {
  color: inherit !important;
  font-family: inherit !important;
  font-size: inherit !important;
  font-style: oblique 12deg !important;
  font-synthesis: style !important;
  font-synthesis-style: auto !important;
  line-height: inherit !important;
  display: inline-block !important;
  transform: skewX(-9deg) !important;
  transform-origin: left bottom !important;
}

.preview-text :deep(i) {
  color: inherit !important;
  font-family: inherit !important;
  font-size: inherit !important;
  font-style: oblique 12deg !important;
  font-synthesis: style !important;
  font-synthesis-style: auto !important;
  line-height: inherit !important;
  display: inline-block !important;
  transform: skewX(-9deg) !important;
  transform-origin: left bottom !important;
}

.preview-text :deep(em::after),
.preview-text :deep(i::after) {
  content: "" !important;
  display: inline-block !important;
  width: 0.14em !important;
}

.preview-text :deep(ruby) {
  color: inherit !important;
  font: inherit !important;
  ruby-position: over !important;
  ruby-align: center !important;
  text-emphasis: none !important;
}

.preview-text :deep(rt) {
  color: inherit !important;
  font-family: inherit !important;
  font-size: 0.44em !important;
  font-weight: 600 !important;
  line-height: 1 !important;
  text-align: center !important;
  text-shadow: inherit !important;
}

.preview-text :deep(rp) {
  display: none !important;
}

.preview-text :deep(.dialog-inside),
.preview-text :deep(.dialog-inside a),
.preview-text :deep(a .dialog-inside),
.preview-text :deep(.dialog-inside a.new) {
  background-color: #252525 !important;
  color: rgba(255, 255, 255, 0) !important;
  -webkit-text-fill-color: rgba(255, 255, 255, 0) !important;
  text-shadow: none !important;
  transition: background-color 0.5s ease, color 0.5s ease, -webkit-text-fill-color 0.5s ease, text-shadow 0.5s ease !important;
}

.preview-text :deep(.dialog-inside:hover),
.preview-text :deep(.dialog-inside:active),
.preview-text :deep(.dialog-inside:hover .dialog-inside),
.preview-text :deep(.dialog-inside:active .dialog-inside) {
  background-color: transparent !important;
  color: inherit !important;
  -webkit-text-fill-color: currentColor !important;
  text-shadow: inherit !important;
}

.preview-text :deep(.dialog-inside:hover a),
.preview-text :deep(a:hover .dialog-inside),
.preview-text :deep(.dialog-inside:active a),
.preview-text :deep(a:active .dialog-inside) {
  background-color: transparent !important;
  color: lightblue !important;
  -webkit-text-fill-color: lightblue !important;
}

.preview-text :deep(.dialog-inside:hover .new),
.preview-text :deep(.dialog-inside .new:hover),
.preview-text :deep(.new:hover .dialog-inside),
.preview-text :deep(.dialog-inside:active .new),
.preview-text :deep(.dialog-inside .new:active),
.preview-text :deep(.new:active .dialog-inside) {
  background-color: transparent !important;
  color: #ba0000 !important;
  -webkit-text-fill-color: #ba0000 !important;
}

.preview-text :deep(del),
.preview-text :deep(s) {
  color: inherit !important;
  font: inherit !important;
  text-decoration: line-through !important;
  text-decoration-thickness: 0.08em !important;
}

.preview-text :deep(u),
.preview-text :deep(ins) {
  color: inherit !important;
  font: inherit !important;
  text-decoration: underline !important;
  text-underline-offset: 0.12em !important;
}

.preview-text :deep(mark) {
  padding: 0 0.12em !important;
  border-radius: 0.12em !important;
  background: rgba(122, 204, 249, 0.34) !important;
  color: #ffffff !important;
  font: inherit !important;
}

.preview-text :deep(a) {
  color: #7accf9 !important;
  font: inherit !important;
  text-decoration: underline !important;
}

.preview-text :deep(code) {
  padding: 0 0.18em !important;
  border-radius: 0.18em !important;
  background: rgba(0, 32, 76, 0.45) !important;
  color: inherit !important;
  font-family: Consolas, "Courier New", monospace !important;
  font-size: 0.88em !important;
}

.preview-text :deep(pre) {
  margin: 0 !important;
  padding: 0.26em 0.42em !important;
  border-radius: 0.2em !important;
  background: rgba(0, 32, 76, 0.52) !important;
  color: inherit !important;
  font-family: Consolas, "Courier New", monospace !important;
  font-size: 0.86em !important;
  line-height: 1.2 !important;
  white-space: pre-wrap !important;
}

.preview-text :deep(pre code) {
  padding: 0 !important;
  background: transparent !important;
  border-radius: 0 !important;
  font: inherit !important;
}

.preview-text :deep(ul),
.preview-text :deep(ol) {
  margin: 0 !important;
  padding-left: 1.2em !important;
  color: inherit !important;
  font: inherit !important;
}

.preview-text :deep(li) {
  margin: 0 !important;
  color: inherit !important;
  font: inherit !important;
}

.preview-text :deep(hr) {
  height: 0.06em !important;
  margin: 0.18em 0 !important;
  border: 0 !important;
  background: rgba(255, 255, 255, 0.76) !important;
}

.preview-text :deep(table) {
  border-collapse: collapse !important;
  color: inherit !important;
  font: inherit !important;
}

.preview-text :deep(th),
.preview-text :deep(td) {
  padding: 0.08em 0.32em !important;
  border: 0.04em solid rgba(255, 255, 255, 0.58) !important;
  color: inherit !important;
  font: inherit !important;
}

.preview-text :deep(blockquote) {
  margin: 0 !important;
  padding-left: 0.5em !important;
  border-left: 0.12em solid rgba(122, 204, 249, 0.8) !important;
  color: inherit !important;
  font: inherit !important;
}

.preview-continue-indicator {
  position: absolute !important;
  left: 92.55% !important;
  top: 92.41% !important;
  width: calc(var(--preview-width, 1920) * 27px / 1920) !important;
  height: calc(var(--preview-width, 1920) * 18px / 1920) !important;
  border-radius: 3px !important;
  background: #4ac4dd !important;
  clip-path: polygon(0 0, 100% 0, 50% 100%) !important;
  filter: drop-shadow(0 3px 5px rgba(0, 0, 0, 0.38)) !important;
  animation: preview-small-rectangle-float 1.05s ease-in-out infinite !important;
}

.preview-choices {
  position: absolute;
  left: 50%;
  bottom: 18%;
  display: flex;
  min-width: 34%;
  flex-direction: column;
  gap: 14px;
  transform: translateX(-50%);
}

.preview-choices button {
  padding: 18px 26px;
  border: 1px solid rgba(255, 255, 255, 0.24);
  border-radius: 12px;
  background: rgba(20, 31, 49, 0.92);
  color: #ffffff;
  font-size: 26px;
  cursor: pointer;
}

.preview-choices button:hover {
  background: rgba(14, 99, 156, 0.96);
}

.preview-status {
  position: absolute;
  left: 50%;
  top: 48px;
  transform: translateX(-50%);
  padding: 12px 18px;
  border-radius: 999px;
  background: rgba(0, 0, 0, 0.52);
  color: #e5e7eb;
  font-size: 22px;
}

.preview-popup-fade-enter-active,
.preview-popup-fade-leave-active {
  transition: opacity 0.16s ease;
}

.preview-popup-fade-enter-from,
.preview-popup-fade-leave-to {
  opacity: 0;
}

@keyframes preview-character-in {
  from {
    opacity: 0;
    translate: 0 18px;
  }
  to {
    opacity: 1;
    translate: 0 0;
  }
}

@keyframes preview-small-rectangle-float {
  0%, 100% {
    transform: translateY(0);
  }
  50% {
    transform: translateY(-10px);
  }
}
</style>