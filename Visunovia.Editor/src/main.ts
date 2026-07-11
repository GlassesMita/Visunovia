import { createApp } from 'vue'
import { createPinia } from 'pinia'
import * as PIXI from 'pixi.js'
import App from './App.vue'
import router from './router'
import { initializeBackendBaseUrl } from './utils/backendUrl'
import { installEditorConsole } from './services/editorConsole'
import './style.css'
import './baklava/styles.css'

async function bootstrap() {
	await installEditorConsole()
	await initializeBackendBaseUrl()

	const app = createApp(App)
	app.config.globalProperties.$pixi = PIXI
	window.PIXI = PIXI

	const pinia = createPinia()
	app.use(pinia)

	app.use(router)

	app.mount('#app')
}

bootstrap()
