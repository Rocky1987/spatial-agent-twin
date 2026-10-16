<script setup lang="ts">
export type NavAction =
  | 'zoomIn' | 'zoomOut'
  | 'rotateLeft' | 'rotateRight'
  | 'tiltUp' | 'tiltDown'
  | 'reset'

const emit = defineEmits<{ (e: 'action', a: NavAction): void }>()

const buttons: { a: NavAction; label: string; title: string }[] = [
  { a: 'zoomIn', label: '＋', title: '放大' },
  { a: 'zoomOut', label: '－', title: '縮小' },
  { a: 'rotateLeft', label: '↺', title: '向左旋轉' },
  { a: 'rotateRight', label: '↻', title: '向右旋轉' },
  { a: 'tiltUp', label: '▲', title: '視角抬高（更俯視）' },
  { a: 'tiltDown', label: '▼', title: '視角放低（更平視）' },
  { a: 'reset', label: '⌂', title: '回到初始視角' },
]
</script>

<template>
  <div class="nav-panel">
    <button v-for="b in buttons" :key="b.a" :title="b.title" @click="emit('action', b.a)">
      {{ b.label }}
    </button>
  </div>
</template>

<style scoped>
.nav-panel {
  position: absolute;
  top: 60px;
  right: 12px;
  z-index: 10;
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.nav-panel button {
  width: 36px;
  height: 36px;
  font-size: 18px;
  cursor: pointer;
  color: #fff;
  background: rgba(30, 40, 55, 0.8);
  border: 1px solid rgba(255, 255, 255, 0.3);
  border-radius: 6px;
}
.nav-panel button:hover {
  background: rgba(60, 80, 110, 0.9);
}
</style>
