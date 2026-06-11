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
                  v-for="character in visibleCharacters"
                  :key="character.slot"
                  class="preview-character"
                  :class="[`preview-character-${character.position}`, `preview-character-${character.animation}`]"
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
const markdownRenderer = new MarkdownIt({
  breaks: true,
  html: false,
  linkify: true,
  typographer: true,
})

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
const dialogueVisible = ref(false)
const textComplete = ref(false)
const statusMessage = ref('')
const previewEnded = ref(false)
const choices = ref<PreviewChoice[]>([])
const characterSlots = ref<Record<string, CharacterSlotState>>({})
const bgmAudio = ref<HTMLAudioElement | null>(null)
const resourceTraceByUrl = ref<Record<string, PreviewResourceTrace>>({})
let typewriterTimer: number | null = null

const visibleCharacters = computed(() => Object.values(characterSlots.value).sort((a, b) => Number(a.slot) - Number(b.slot)))

const renderedDialogueHtml = computed(() => DOMPurify.sanitize(markdownRenderer.render(dialogueText.value)))

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

watch(
  () => props.visible,
  async (visible) => {
    if (visible) {
      await characterStore.load().catch((error) => console.warn('[PreviewPopup] 角色配置加载失败', error))
      await nextTick()
      restartPreview()
    } else {
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
  stopTypewriter()
  stopAudio()
})

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
  textComplete.value = text.length === 0

  if (!text) return

  let index = 0
  const step = () => {
    index += 1
    dialogueText.value = text.slice(0, index)
    if (index >= text.length) {
      textComplete.value = true
      typewriterTimer = null
      return
    }
    typewriterTimer = window.setTimeout(step, getTypewriterDelay(text[index]))
  }

  typewriterTimer = window.setTimeout(step, getTypewriterDelay(text[0]))
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
  textComplete.value = true
}

function getTypewriterDelay(character: string) {
  return /[，。！？、,.!?]/.test(character) ? 86 : 28
}

function getDialogueSpeaker(node: PreviewNode) {
  const directSpeaker = String(node.data.speaker || node.data.character || '').trim()
  if (directSpeaker) return directSpeaker

  const controls = getCharacterControlsForDialogue(node)
  return String(controls.find((control: any) => control?.character)?.character || '').trim()
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

    const sprite = String(control.sprite || nextSlots[slot]?.sprite || '')
    const spriteUrl = resolveAssetUrl(sprite, 'Characters')
    registerResourceTrace(createResourceTrace(dialogueNode, 'character', `角色立绘 slot ${slot}`, 'characterControls.sprite', sprite, spriteUrl))
    nextSlots[slot] = {
      slot,
      character: String(control.character || nextSlots[slot]?.character || ''),
      sprite,
      spriteUrl,
      position: String(control.position || nextSlots[slot]?.position || 'center'),
      animation: String(control.animation || 'fade'),
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
        slot: slotFromPort || controlNode.data.slot || '1',
        ...controlNode.data,
      }
    })
    .filter(Boolean)

  return [...inlineControls, ...linkedControls]
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
  return path.split(/[?#]/)[0].toLowerCase().endsWith('.mp4')
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
  bottom: 6% !important;
  max-height: 86% !important;
  max-width: 42% !important;
  object-fit: contain !important;
  opacity: 1 !important;
  mix-blend-mode: normal !important;
  filter: drop-shadow(0 18px 28px rgba(0, 0, 0, 0.45)) !important;
}

.preview-character-left {
  left: 12%;
  transform: translateX(-50%);
}

.preview-character-center {
  left: 50%;
  transform: translateX(-50%);
}

.preview-character-right {
  right: 12%;
  transform: translateX(50%);
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

.preview-text :deep(p + p) {
  margin-top: 0.35em !important;
}

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
  font-style: italic !important;
  line-height: inherit !important;
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

.preview-text :deep(ul),
.preview-text :deep(ol) {
  margin: 0 !important;
  padding-left: 1.2em !important;
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