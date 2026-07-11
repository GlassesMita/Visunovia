<template>
  <main class="player-shell">
    <section class="player-stage" aria-label="Visunovia Player">
      <div class="player-brand">Visunovia Player</div>
      <p>项目播放运行时已启动。</p>
    </section>
  </main>
</template>

<style scoped>
.player-shell {
  min-height: 100vh;
  display: grid;
  place-items: center;
  background: #101820;
  color: #f1f5f9;
  font-family: "Cascadia Code", "Microsoft YaHei UI", "Noto Sans CJK SC", sans-serif;
}

.player-stage {
  width: min(100%, 1280px);
  aspect-ratio: 16 / 9;
  display: grid;
  place-items: center;
  align-content: center;
  gap: 12px;
  background: #1b2a36;
}

.player-brand {
  font-size: 28px;
  font-weight: 700;
}

p {
  margin: 0;
  color: #bdd5e8;
}
</style>