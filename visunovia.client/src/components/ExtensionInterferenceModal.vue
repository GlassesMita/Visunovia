<template>
  <Teleport to="body">
    <div v-if="visible" class="extension-interference-overlay" role="presentation">
      <section class="extension-interference-modal" role="dialog" aria-modal="true" :aria-labelledby="titleId">
        <header>
          <h2 :id="titleId">{{ t('extensionInterference.title', 'Display interference detected') }}</h2>
        </header>
        <p>{{ t('extensionInterference.message', 'A browser extension or forced color rule appears to be changing the application colors. Disable the extension or add this app to its allowlist to restore the intended theme.') }}</p>
        <div class="extension-interference-details">
          {{ t('extensionInterference.expected', 'Expected') }}: {{ expectedColor }} ·
          {{ t('extensionInterference.actual', 'Actual') }}: {{ actualColor }}
        </div>
        <footer>
          <button type="button" @click="dismissForSession">
            {{ t('extensionInterference.dismiss', 'Dismiss') }}
          </button>
          <button type="button" class="primary" @click="checkNow">
            {{ t('extensionInterference.recheck', 'Check again') }}
          </button>
        </footer>
      </section>
    </div>
  </Teleport>
</template>

<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { useLocalization } from '@/composables/useLocalization'

const { t } = useLocalization()
const titleId = 'extension-interference-title'
const visible = ref(false)
const dismissed = ref(false)
const expectedColor = ref('')
const actualColor = ref('')
let checkTimer: number | null = null

function normalizeColor(value: string) {
  const probe = document.createElement('span')
  probe.style.color = value
  document.body.appendChild(probe)
  const normalized = getComputedStyle(probe).color.replace(/\s+/g, '')
  probe.remove()
  return normalized
}

function checkInterference() {
  if (dismissed.value || !document.body) return

  const theme = document.documentElement.dataset.theme === 'light' ? 'light' : 'dark'
  const expected = theme === 'light' ? 'rgb(0,0,0)' : 'rgb(255,255,255)'
  const probe = document.createElement('span')
  probe.className = 'extension-interference-probe'
  probe.textContent = 'Visunovia'
  probe.style.cssText = [
    'position:fixed',
    'left:-9999px',
    'top:-9999px',
    'z-index:-1',
    `color:${expected}!important`,
    `-webkit-text-fill-color:${expected}!important`,
    'forced-color-adjust:none!important',
  ].join(';')

  document.body.appendChild(probe)
  const computed = getComputedStyle(probe)
  const color = computed.color.replace(/\s+/g, '')
  const fill = (computed as any).webkitTextFillColor?.replace(/\s+/g, '') || color
  probe.remove()

  expectedColor.value = expected
  actualColor.value = fill || color
  visible.value = color !== normalizeColor(expected) || fill !== normalizeColor(expected)
}

function checkNow() {
  visible.value = false
  window.setTimeout(checkInterference, 120)
}

function dismissForSession() {
  dismissed.value = true
  visible.value = false
}

onMounted(() => {
  checkTimer = window.setTimeout(checkInterference, 600)
  window.addEventListener('focus', checkInterference)
})

onBeforeUnmount(() => {
  if (checkTimer !== null) window.clearTimeout(checkTimer)
  window.removeEventListener('focus', checkInterference)
})
</script>

<style scoped>
.extension-interference-overlay {
  position: fixed;
  inset: 0;
  z-index: 2147483646;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 24px;
  background: rgba(0, 0, 0, 0.58);
}

.extension-interference-modal {
  width: min(520px, 100%);
  border: 1px solid var(--vn-border-strong);
  border-radius: 16px;
  padding: 24px;
  background: var(--vn-surface);
  color: var(--vn-text);
  box-shadow: 0 24px 80px rgba(0, 0, 0, 0.38);
}

.extension-interference-modal h2 {
  margin: 0 0 12px;
  color: var(--vn-text);
  font-size: 20px;
}

.extension-interference-modal p {
  margin: 0;
  color: var(--vn-text-muted);
  line-height: 1.6;
}

.extension-interference-details {
  margin-top: 16px;
  border-radius: 10px;
  padding: 10px 12px;
  background: var(--vn-surface-muted);
  color: var(--vn-text-muted);
  font-size: 13px;
}

.extension-interference-modal footer {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  margin-top: 22px;
}

.extension-interference-modal button {
  border: 1px solid var(--vn-border-strong);
  border-radius: 8px;
  padding: 8px 14px;
  background: var(--vn-control-bg);
  color: var(--vn-text);
  cursor: pointer;
}

.extension-interference-modal button.primary {
  border-color: var(--vn-accent);
  background: var(--vn-accent);
  color: #ffffff;
}
</style>
