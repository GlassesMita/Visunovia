type ChiptunePlayer = import('chiptune3').ChiptuneJsPlayer

export class ModPlayer {
  private player: ChiptunePlayer | null = null
  private initialization: Promise<ChiptunePlayer> | null = null
  private loadVersion = 0

  async play(url: string, volume = 1, loop = true) {
    const version = ++this.loadVersion
    const player = await this.getPlayer()
    if (version !== this.loadVersion) return

    player.stop()
    player.setVol(clamp(volume))
    player.setRepeatCount(loop ? -1 : 0)
    player.load(url)
  }

  pause() {
    this.player?.pause()
  }

  resume() {
    this.player?.unpause()
  }

  seek(seconds: number) {
    this.player?.setPos(Math.max(0, Number(seconds) || 0))
  }

  setVolume(volume: number) {
    this.player?.setVol(clamp(volume))
  }

  stop() {
    this.loadVersion += 1
    this.player?.stop()
  }

  private getPlayer() {
    if (this.player) return Promise.resolve(this.player)
    if (this.initialization) return this.initialization

    this.initialization = import('chiptune3').then(({ ChiptuneJsPlayer }) => new Promise<ChiptunePlayer>((resolve, reject) => {
      const player = new ChiptuneJsPlayer({ repeatCount: -1 })
      player.onInitialized(() => {
        this.player = player
        resolve(player)
      })
      player.onError((error) => reject(new Error(`libopenmpt: ${error?.type || 'unknown error'}`)))
    })).catch((error) => {
      this.initialization = null
      throw error
    })

    return this.initialization
  }
}

function clamp(value: number) {
  return Math.max(0, Math.min(1, Number(value) || 0))
}