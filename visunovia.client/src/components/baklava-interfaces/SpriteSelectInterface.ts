import { NodeInterface } from '@baklavajs/core'
import { defineComponent, h, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import NativeFreeSelect from '@/components/NativeFreeSelect.vue'
import { getEntries } from '@/api/fileBrowser'
import { useCharacterStore } from '@/stores/useCharacterStore'
import { resolveAssetUrl } from '@/utils/assetPaths'

interface SpriteOption {
  name: string
  path: string
  thumbnail: string
}

const IMAGE_EXTENSIONS = ['.png', '.jpg', '.jpeg', '.webp', '.gif', '.bmp', '.svg']

function isImageFile(name: string) {
  const dotIndex = name.lastIndexOf('.')
  return dotIndex >= 0 && IMAGE_EXTENSIONS.includes(name.slice(dotIndex).toLowerCase())
}

function toPreviewUrl(path: string) {
  return resolveAssetUrl(path, 'Characters')
}

function getInterfaceValue(intf: any, key: string) {
  const sibling = intf?.node?.inputs?.[key]
  return sibling?.value ?? ''
}

function setInterfaceValue(intf: any, key: string, value: string) {
  const sibling = intf?.node?.inputs?.[key]
  if (!sibling) return

  if (typeof sibling.setValue === 'function') {
    sibling.setValue(value)
  } else {
    sibling.value = value
  }
}

const SpriteSelectComponent = defineComponent({
  name: 'SpriteSelectInterfaceComponent',
  props: {
    intf: {
      type: Object,
      required: true,
    },
  },
  setup(props: any) {
    const characterStore = useCharacterStore()
    const options = ref<SpriteOption[]>([])
    const loading = ref(false)
    const error = ref('')
    const selectedCharacterId = ref('')
    const selectedSlot = ref('')
    const subscriptionToken = {}

    async function loadSprites() {
      const characterId = String(getInterfaceValue(props.intf, 'character') || '').trim()
      const slot = String(getInterfaceValue(props.intf, 'slot') || '').trim()
      selectedCharacterId.value = characterId
      selectedSlot.value = slot

      if (slot === '6' || !characterId) {
        options.value = []
        return
      }

      await characterStore.load()
      const character = characterStore.sortedCharacters.find(item => item.id === characterId)
      const spriteFolder = character?.spriteFolder?.trim()
      if (!spriteFolder) {
        options.value = []
        return
      }

      loading.value = true
      error.value = ''
      try {
        const result = await getEntries(spriteFolder)
        options.value = result.entries
          .filter(entry => !entry.isDirectory && isImageFile(entry.name))
          .sort((a, b) => a.name.localeCompare(b.name, 'zh-CN'))
          .map(entry => ({
            name: entry.name,
            path: entry.path,
            thumbnail: toPreviewUrl(entry.path),
          }))
      } catch (err: any) {
        error.value = err?.message || '读取立绘失败'
        options.value = []
      } finally {
        loading.value = false
      }
    }

    function selectSprite(path: string) {
      if (typeof props.intf.setValue === 'function') {
        props.intf.setValue(path)
      } else {
        props.intf.value = path
      }
    }

    function clearSpriteForSlot6() {
      if (String(getInterfaceValue(props.intf, 'slot') || '').trim() === '6' && props.intf.value) {
        selectSprite('')
      }
    }

    function subscribeToSiblingUpdates() {
      const inputs = props.intf?.node?.inputs || {}
      ;[inputs.character, inputs.slot].forEach((input: any) => {
        input?.events?.setValue?.subscribe?.(subscriptionToken, () => {
          loadSprites().then(clearSpriteForSlot6)
        })
      })
    }

    function unsubscribeFromSiblingUpdates() {
      const inputs = props.intf?.node?.inputs || {}
      ;[inputs.character, inputs.slot].forEach((input: any) => {
        input?.events?.setValue?.unsubscribe?.(subscriptionToken)
      })
    }

    onMounted(() => {
      subscribeToSiblingUpdates()
      loadSprites().then(clearSpriteForSlot6)
    })

    onBeforeUnmount(unsubscribeFromSiblingUpdates)

    watch(
      () => [getInterfaceValue(props.intf, 'character'), getInterfaceValue(props.intf, 'slot')],
      () => {
        loadSprites().then(clearSpriteForSlot6)
      },
      { flush: 'post' }
    )

    return () => {
      const slot = String(selectedSlot.value || '').trim()
      const isSlot6 = slot === '6'
      const unmanagedName = String(getInterfaceValue(props.intf, 'unmanagedCharacter') || '').trim()
      const spriteOptions = [
        { value: '', label: loading.value ? '读取立绘中...' : '未选择立绘' },
        ...options.value.map(option => ({ value: option.path, label: option.name })),
      ]

      return h('div', { class: 'vn-sprite-select' }, [
        isSlot6
          ? h('div', { class: 'vn-sprite-slot6' }, [
              h('span', 'Slot 6 为无立绘角色'),
              h('input', {
                class: 'vn-sprite-slot6-name',
                placeholder: '角色名',
                value: unmanagedName,
                onInput: (event: Event) => setInterfaceValue(props.intf, 'unmanagedCharacter', (event.target as HTMLInputElement).value),
              }),
            ])
          : h(NativeFreeSelect, {
              class: 'vn-sprite-select-control',
              modelValue: props.intf.value || '',
              options: spriteOptions,
              disabled: loading.value || options.value.length === 0,
              'onUpdate:modelValue': selectSprite,
              onChange: selectSprite,
            }),
        !isSlot6 && props.intf.value
          ? h('img', {
              class: 'vn-sprite-select-preview',
              src: toPreviewUrl(String(props.intf.value)),
              alt: 'sprite preview',
            })
          : null,
        !isSlot6 && options.value.length > 0
          ? h('div', { class: 'vn-sprite-select-grid' }, options.value.map(option => h('button', {
              type: 'button',
              class: ['vn-sprite-select-option', { selected: option.path === props.intf.value }],
              title: option.name,
              onClick: () => selectSprite(option.path),
            }, [
              h('img', { src: option.thumbnail, alt: option.name, loading: 'lazy' }),
              h('span', option.name),
            ])))
          : null,
        !isSlot6 && error.value ? h('div', { class: 'vn-sprite-select-error' }, error.value) : null,
      ])
    }
  },
})

export class SpriteSelectInterface extends NodeInterface<string> {
  constructor(name: string, value = '') {
    super(name, value)
    this.setComponent(SpriteSelectComponent)
  }
}