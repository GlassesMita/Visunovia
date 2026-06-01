<template>
  <div class="about-page">
    <div class="about-container">
      <div class="app-header">
        <h1>{{ t('app.title') }}</h1>
        <p class="version">Version {{ appVersion }}</p>
      </div>

      <div class="about-section">
        <h2>Description</h2>
        <p class="description">
          A visual novel editor powered by Vue 3 and BaklavaJS.
          Create interactive visual novels with an intuitive node-based graph editor.
        </p>
      </div>

      <div class="about-section">
        <h2>Technology Stack</h2>
        <ul class="tech-list">
          <li v-for="tech in techStack" :key="tech.name">
            <span class="tech-name">{{ tech.name }}</span>
            <span class="tech-version">{{ tech.version }}</span>
          </li>
        </ul>
      </div>

      <div class="about-section">
        <h2>Features</h2>
        <ul class="feature-list">
          <li v-for="feature in features" :key="feature">
            {{ feature }}
          </li>
        </ul>
      </div>

      <div class="about-section">
        <h2>License</h2>
        <p class="license">
          This project is licensed under the MIT License.
        </p>
        <p class="license-text">
          Copyright (c) 2024 Visunovia Team. All rights reserved.
        </p>
      </div>

      <div class="about-section">
        <h2>Links</h2>
        <div class="links">
          <a
            v-for="link in links"
            :key="link.name"
            :href="link.url"
            target="_blank"
            rel="noopener noreferrer"
            class="link-item"
          >
            <span class="link-icon">{{ link.icon }}</span>
            <span>{{ link.name }}</span>
          </a>
        </div>
      </div>

      <div class="about-footer">
        <p>Built with ❤️ by the Visunovia Team</p>
        <p class="build-info">Build Date: {{ buildDate }}</p>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useLocalization } from '@/composables/useLocalization'

const { t } = useLocalization()

const appVersion = '0.1.0-alpha'
const buildDate = new Date().toISOString().split('T')[0]

const techStack = [
  { name: 'Vue 3', version: '3.x' },
  { name: 'TypeScript', version: '5.x' },
  { name: 'BaklavaJS', version: '2.x' },
  { name: 'Pinia', version: '2.x' },
  { name: 'Vue Router', version: '4.x' },
  { name: 'ASP.NET Core', version: '8.x' },
]

const features = [
  'Node-based visual scripting',
  'Visual novel scene editing',
  'Character sprite management',
  'Background and BGM control',
  'Dialogue system with branching',
  'Undo/Redo support',
  'Multi-language support (i18n)',
  'Dark/Light theme support',
  'Keyboard shortcuts',
  'Project persistence'
]

const links = [
  {
    name: 'GitHub Repository',
    url: 'https://github.com/visunovia/visunovia-editor',
    icon: '📦'
  },
  {
    name: 'Documentation',
    url: 'https://docs.visunovia.dev',
    icon: '📚'
  },
  {
    name: 'Issue Tracker',
    url: 'https://github.com/visunovia/visunovia-editor/issues',
    icon: '🐛'
  },
  {
    name: 'Community Discord',
    url: 'https://discord.gg/visunovia',
    icon: '💬'
  }
]
</script>

<style scoped>
.about-page {
  display: flex;
  justify-content: center;
  padding: 40px;
  background: #1e1e1e;
  min-height: 100vh;
  overflow-y: auto;
  box-sizing: border-box;
}

.about-container {
  width: 100%;
  max-width: 650px;
  background: #252526;
  border-radius: 8px;
  padding: 32px;
}

.app-header {
  text-align: center;
  margin-bottom: 32px;
  padding-bottom: 24px;
  border-bottom: 1px solid #3e3e42;
}

.app-header h1 {
  margin: 0 0 8px;
  font-size: 28px;
  color: #ffffff;
}

.version {
  color: #888;
  font-size: 14px;
  margin: 0;
}

.about-section {
  margin-bottom: 28px;
  padding-bottom: 20px;
  border-bottom: 1px solid #3e3e42;
}

.about-section:last-of-type {
  border-bottom: none;
  margin-bottom: 0;
}

.about-section h2 {
  margin: 0 0 14px;
  font-size: 16px;
  font-weight: 600;
  color: #cccccc;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.description {
  color: #d4d4d4;
  font-size: 14px;
  line-height: 1.6;
  margin: 0;
}

.tech-list,
.feature-list {
  list-style: none;
  padding: 0;
  margin: 0;
}

.tech-list li {
  display: flex;
  justify-content: space-between;
  padding: 8px 12px;
  border-radius: 4px;
  margin-bottom: 6px;
  background: #2d2d30;
}

.tech-name {
  color: #ffffff;
  font-weight: 500;
  font-size: 14px;
}

.tech-version {
  color: #888;
  font-size: 13px;
}

.feature-list li {
  padding: 6px 0;
  color: #d4d4d4;
  font-size: 14px;
  position: relative;
  padding-left: 20px;
}

.feature-list li::before {
  content: '✓';
  position: absolute;
  left: 0;
  color: #4ec9b0;
  font-weight: bold;
}

.license {
  color: #d4d4d4;
  font-size: 14px;
  margin: 0 0 8px;
}

.license-text {
  color: #888;
  font-size: 13px;
  margin: 0;
}

.links {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.link-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 14px;
  background: #2d2d30;
  border-radius: 4px;
  color: #4fc3f7;
  text-decoration: none;
  font-size: 14px;
  transition: background 0.2s ease, transform 0.1s ease;
}

.link-item:hover {
  background: #3e3e42;
  transform: translateX(4px);
}

.link-icon {
  font-size: 18px;
}

.about-footer {
  margin-top: 28px;
  padding-top: 20px;
  border-top: 1px solid #3e3e42;
  text-align: center;
}

.about-footer p {
  color: #666;
  font-size: 13px;
  margin: 4px 0;
}

.build-info {
  font-size: 11px !important;
}
</style>
