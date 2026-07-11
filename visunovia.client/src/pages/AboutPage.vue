<template>
  <div class="about-root">
    <nav class="about-sidebar">
      <div class="sidebar-header">
        <Info :size="20" />
        <span>{{ t('menu.about') || 'About' }}</span>
      </div>
      <button type="button" class="sidebar-item" @click="scrollToSection('software')">{{ t('About.Sidebar.SoftwareInfo') || 'Software Info' }}</button>
      <button type="button" class="sidebar-item" @click="scrollToSection('tech')">{{ t('About.Sidebar.TechnologyStack') || 'Technology Stack' }}</button>
      <button type="button" class="sidebar-item" @click="scrollToSection('licenses')">{{ t('About.Sidebar.OpenSourceLicenses') || 'Open Source Licenses' }}</button>
      <button type="button" class="sidebar-item" @click="scrollToSection('links')">{{ t('About.Sidebar.Links') || 'Links' }}</button>
    </nav>

    <main class="about-content">
      <section id="software" class="about-section hero-section">
        <div class="app-mark">V</div>
        <div>
          <h1>Visunovia</h1>
          <p>{{ aboutDescription }}</p>
          <div class="meta-grid">
            <div>
              <span>{{ t('About.Label.Version') || 'Version' }}</span>
              <strong>1.0.0</strong>
            </div>
            <div>
              <span>{{ t('About.Label.Platform') || 'Platform' }}</span>
              <strong>Electron / Windows</strong>
            </div>
            <div>
              <span>{{ t('About.Label.License') || 'License' }}</span>
              <strong>GPL-3.0-only (Editor)</strong>
            </div>
          </div>
        </div>
      </section>

      <section id="tech" class="about-section">
        <h2>{{ t('About.Section.TechnologyStack') || 'Technology Stack' }}</h2>
        <div class="tech-grid">
          <div v-for="item in techStack" :key="item.name" class="tech-item">
            <strong>{{ item.name }}</strong>
            <span>{{ item.version }}</span>
          </div>
        </div>
      </section>

      <section id="licenses" class="about-section">
        <h2>{{ t('About.Section.OpenSourceLicenses') || 'Open Source Licenses' }}</h2>
        <p>{{ t('About.LicenseIntro') || 'This software is built with open source components.' }}</p>
        <div class="license-list">
          <a
            v-for="item in licenses"
            :key="item.name"
            class="license-item"
            :href="item.github"
            :title="item.github"
            @click.prevent="openLibraryLink(item.github)"
          >
            <span class="license-name">
              {{ item.name }}
              <ExternalLink :size="14" aria-hidden="true" />
            </span>
            <strong>{{ item.license }}</strong>
          </a>
        </div>
      </section>

      <section id="links" class="about-section">
        <h2>{{ t('About.Section.Links') || 'Links' }}</h2>
        <p>{{ t('About.LicenseText') || 'Copyright (c) 2026 Visunovia Team.' }}</p>
      </section>
    </main>
  </div>
</template>

<script setup lang="ts">
import { ExternalLink, Info } from 'lucide-vue-next'
import { useLocalization } from '@/composables/useLocalization'

const { t } = useLocalization()

function scrollToSection(sectionId: string) {
  document.getElementById(sectionId)?.scrollIntoView({ behavior: 'smooth', block: 'start' })
}

async function openLibraryLink(url: string) {
  if (window.visunoviaDesktop?.openExternal) {
    await window.visunoviaDesktop.openExternal(url)
    return
  }
  window.open(url, '_blank', 'noopener,noreferrer')
}

const aboutDescription = t('About.Description') === 'About.Description'
  ? 'A modern visual novel editor built with Vue 3, TypeScript, Electron, and a local JavaScript backend.'
  : t('About.Description')

const techStack = [
  { name: 'Vue', version: '3.x' },
  { name: 'TypeScript', version: '5.x' },
  { name: 'Pinia', version: '2.x' },
  { name: 'Vue Router', version: '4.x' },
  { name: 'Vite', version: '5.x' },
  { name: 'Electron', version: '43.x' },
  { name: 'BaklavaJS', version: '2.x' },
  { name: 'PixiJS', version: '8.x' },
]

const licenses = [
  { name: 'Vue', license: 'MIT', github: 'https://github.com/vuejs/core' },
  { name: 'TypeScript', license: 'Apache-2.0', github: 'https://github.com/microsoft/TypeScript' },
  { name: 'Pinia', license: 'MIT', github: 'https://github.com/vuejs/pinia' },
  { name: 'Vue Router', license: 'MIT', github: 'https://github.com/vuejs/router' },
  { name: 'Vite', license: 'MIT', github: 'https://github.com/vitejs/vite' },
  { name: 'Electron', license: 'MIT', github: 'https://github.com/electron/electron' },
  { name: 'Lucide', license: 'ISC', github: 'https://github.com/lucide-icons/lucide' },
  { name: 'BaklavaJS', license: 'MIT', github: 'https://github.com/newcat/baklavajs' },
  { name: 'PixiJS', license: 'MIT', github: 'https://github.com/pixijs/pixijs' },
]
</script>

<style scoped>
.about-root {
  display: flex;
  height: 100vh;
  overflow: hidden;
  background: var(--vn-bg);
  color: var(--vn-text);
}

.about-sidebar {
  width: 220px;
  flex-shrink: 0;
  background: color-mix(in srgb, var(--md-sys-color-surface-container) 92%, transparent);
  border-right: 1px solid var(--vn-border);
  overflow-y: auto;
}

.sidebar-header {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 16px;
  color: var(--vn-text-soft);
  font-size: 13px;
  font-weight: 600;
  text-transform: uppercase;
  border-bottom: 1px solid var(--vn-border);
}

.sidebar-item {
  display: block;
  width: 100%;
  padding: 11px 16px;
  color: var(--vn-text-muted);
  font-size: 13px;
  text-decoration: none;
  text-align: left;
  border-left: 3px solid transparent;
}

.sidebar-item:hover {
  color: var(--vn-text);
  background: var(--vn-surface-muted);
  border-left-color: var(--md-sys-color-primary);
}

.about-content {
  flex: 1;
  min-width: 0;
  min-height: 0;
  overflow-x: hidden;
  overflow-y: auto;
  padding: 36px 44px;
  scroll-behavior: smooth;
}

.about-section {
  max-width: 860px;
  margin-bottom: 34px;
  scroll-margin-top: 36px;
}

.hero-section {
  display: flex;
  gap: 22px;
  align-items: flex-start;
}

.app-mark {
  display: grid;
  place-items: center;
  width: 68px;
  height: 68px;
  border-radius: 8px;
  background: var(--md-sys-color-primary);
  color: var(--md-sys-color-on-primary);
  font-size: 34px;
  font-weight: 700;
}

h1,
h2 {
  margin: 0 0 12px;
}

p {
  margin: 0 0 18px;
  color: var(--vn-text-muted);
  line-height: 1.6;
}

.meta-grid,
.tech-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
  gap: 12px;
}

.meta-grid > div,
.tech-item,
.license-item {
  padding: 14px;
  border: 1px solid var(--vn-border);
  border-radius: 8px;
  background: var(--vn-surface);
}

.meta-grid span,
.tech-item span {
  display: block;
  margin-bottom: 6px;
  color: var(--vn-text-muted);
  font-size: 12px;
}

.license-list {
  display: grid;
  gap: 8px;
}

.license-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  color: inherit;
  text-decoration: none;
  transition: border-color 0.15s, background 0.15s;
}

.license-item:hover,
.license-item:focus-visible {
  background: var(--vn-surface-muted);
  border-color: var(--md-sys-color-primary);
}

.license-item:focus-visible {
  outline: 2px solid var(--md-sys-color-primary);
  outline-offset: 2px;
}

.license-name {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}
</style>