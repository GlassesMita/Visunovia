/// <reference types="vite/client" />

import type * as PIXI from 'pixi.js'

declare module '*.vue' {
  import type { DefineComponent } from 'vue'
  const component: DefineComponent<object, object, unknown>
  export default component
}

declare global {
  interface Window {
    PIXI: typeof PIXI
    visunoviaDesktop?: {
      openProjectFromWelcome(projectPath: string): Promise<void>
      openEditorFromWelcome(): Promise<void>
      executeStageCommand(command: Record<string, unknown>): Promise<void>
      emitExtensionEvent(event: Record<string, unknown>): Promise<void>
    }
  }
}

declare module '@vue/runtime-core' {
  interface ComponentCustomProperties {
    $pixi: typeof PIXI
  }
}

export {}
