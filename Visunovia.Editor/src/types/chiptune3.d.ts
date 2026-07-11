declare module 'chiptune3' {
  export class ChiptuneJsPlayer {
    constructor(config?: {
      repeatCount?: number
      stereoSeparation?: number
      interpolationFilter?: number
      context?: AudioContext
    })

    onInitialized(handler: () => void): void
    onError(handler: (error: { type?: string }) => void): void
    load(url: string): void
    stop(): void
    pause(): void
    unpause(): void
    setRepeatCount(value: number): void
    setPos(seconds: number): void
    setVol(value: number): void
  }
}