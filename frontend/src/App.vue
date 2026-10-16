<script setup lang="ts">
import { ref } from 'vue'
import CesiumView from './components/CesiumView.vue'
import BimView from './components/BimView.vue'
import NavPanel from './components/NavPanel.vue'

// 雙視窗：地圖固定在左邊；split 時右邊展開 BIM 窗格
const split = ref(false)
const status = ref('')
const model = ref('/models/AC20-FZK-Haus.ifc')

const cesiumRef = ref<InstanceType<typeof CesiumView>>()
const bimRef = ref<InstanceType<typeof BimView>>()

// 分隔條：拖曳調整 BIM 窗格寬度（佔整體的 20–80%）
const bimPct = ref(50)
function dragStart(e: PointerEvent) {
  const bar = e.currentTarget as HTMLElement
  bar.setPointerCapture(e.pointerId)
  const move = (ev: PointerEvent) => {
    bimPct.value = Math.min(80, Math.max(20, ((window.innerWidth - ev.clientX) / window.innerWidth) * 100))
  }
  const up = () => {
    bar.removeEventListener('pointermove', move)
    bar.removeEventListener('pointerup', up)
  }
  bar.addEventListener('pointermove', move)
  bar.addEventListener('pointerup', up)
}

const FADE_MS = 700
const sleep = (ms: number) => new Promise<void>((r) => setTimeout(r, ms))

// Stage 4 的工具：每個都回傳 Promise，轉場完成才結束
async function flyToLocation(place: 'institute' | 'taiwan') {
  if (place === 'institute') {
    await cesiumRef.value?.flyToTarget()
    split.value = true
    await sleep(FADE_MS)
  } else {
    split.value = false
    await sleep(FADE_MS)
    await cesiumRef.value?.flyHome()
  }
}
</script>

<template>
  <div class="stage" :class="{ split }">
    <div class="pane pane-map">
      <CesiumView ref="cesiumRef" :active="true" @status="status = $event" />
      <NavPanel @action="cesiumRef?.navigate($event)" />
      <div class="tag">城市</div>
    </div>
    <div class="divider" title="拖曳調整視窗大小" @pointerdown="dragStart"><span /></div>
    <div class="pane pane-bim" :style="{ '--bim-w': bimPct + '%' }">
      <BimView ref="bimRef" :active="split" :model-url="model" @status="status = $event" />
      <NavPanel @action="bimRef?.navigate($event)" />
      <div class="tag">建築內部（BIM）</div>
      <button class="close" title="關閉 BIM 視窗" @click="split = false">✕</button>
    </div>

    <button class="edge" :title="split ? '收合 BIM 視窗' : '開啟 BIM 視窗'" @click="split = !split">
      {{ split ? '▶' : '◀' }}
    </button>

    <div class="toolbar">
      <button @click="flyToLocation('institute')">飛往大樓</button>
      <button @click="flyToLocation('taiwan')">回到台灣</button>
      <span class="status">{{ status }}</span>
    </div>
  </div>
</template>

<style scoped>
.stage {
  position: fixed;
  inset: 0;
  display: flex;
}
.pane {
  position: relative;
  overflow: hidden;
}
.pane-map {
  flex: 1 1 0;
  min-width: 0;
}
.pane-bim {
  flex: 0 0 0;
  opacity: 0;
  pointer-events: none;
  transition: opacity 0.7s ease;
}
.split .pane-bim {
  flex: 0 0 var(--bim-w, 50%);
  opacity: 1;
  pointer-events: auto;
}
.divider {
  flex: 0 0 0;
  width: 0;
  overflow: hidden;
  position: relative;
  z-index: 15;
  cursor: col-resize;
  touch-action: none;
  background: linear-gradient(90deg, #0b1118, #2a3a4f 50%, #0b1118);
  display: flex;
  align-items: center;
  justify-content: center;
  transition: background 0.15s;
}
.split .divider {
  flex-basis: 10px;
  width: 10px;
}
.divider:hover {
  background: linear-gradient(90deg, #0b1118, #4a82c4 50%, #0b1118);
}
.divider span {
  width: 4px;
  height: 40px;
  border-radius: 2px;
  background: rgba(255, 255, 255, 0.55);
}
.edge {
  position: absolute;
  top: 50%;
  right: 0;
  transform: translateY(-50%);
  z-index: 20;
  width: 22px;
  height: 64px;
  cursor: pointer;
  color: #fff;
  background: rgba(30, 40, 55, 0.85);
  border: 1px solid rgba(255, 255, 255, 0.3);
  border-right: none;
  border-radius: 8px 0 0 8px;
}
.edge:hover {
  background: rgba(60, 80, 110, 0.95);
}
.close {
  position: absolute;
  top: 12px;
  right: 12px;
  z-index: 10;
  width: 36px;
  height: 36px;
  font-size: 16px;
  cursor: pointer;
  color: #fff;
  background: rgba(150, 40, 40, 0.85);
  border: 1px solid rgba(255, 255, 255, 0.3);
  border-radius: 6px;
}
.close:hover {
  background: rgba(200, 50, 50, 0.95);
}
.tag {
  position: absolute;
  left: 12px;
  bottom: 96px;
  z-index: 10;
  color: #fff;
  background: rgba(0, 0, 0, 0.5);
  padding: 2px 8px;
  border-radius: 4px;
  font-size: 12px;
  pointer-events: none;
}
.toolbar {
  position: absolute;
  top: 12px;
  left: 12px;
  z-index: 20;
  display: flex;
  gap: 12px;
  align-items: center;
}
.toolbar button {
  padding: 6px 14px;
  cursor: pointer;
}
.status {
  color: #fff;
  background: rgba(0, 0, 0, 0.5);
  padding: 2px 8px;
  border-radius: 4px;
  font-size: 12px;
}
</style>
