<script setup lang="ts">
import { ref } from 'vue'
import spaceMap from './data/space-map.json'
import CesiumView from './components/CesiumView.vue'
import BimView from './components/BimView.vue'
import NavPanel from './components/NavPanel.vue'
import Button from './components/ui/Button.vue'
import UiSelect from './components/ui/UiSelect.vue'
import { ChevronLeft, ChevronRight, Eraser, Layers, MapPinned, Plane, RotateCcw, ScanSearch, X } from 'lucide-vue-next'

// 雙視窗：地圖固定在左邊；split 時右邊展開 BIM 窗格
const split = ref(false)
const status = ref('')
const model = ref('/models/AC20-Institute-Var-2.ifc')
const pickInfo = ref('')
const storeyKey = ref(spaceMap.storeys[1].key)
const spaceKey = ref(spaceMap.spaces[0].key)

const storeyOptions = spaceMap.storeys.map((s) => ({ value: s.key, label: s.zh }))
const spaceOptions = spaceMap.spaces.map((s) => ({ value: s.key, label: `${s.zh}（${s.name}）` }))

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

// 展開 BIM 窗格並等模型載入完成；使用者可能已把窗格收合，所以每個 BIM 工具都先呼叫它
async function openBim() {
  if (!split.value) {
    split.value = true
    await sleep(FADE_MS)
  }
  await bimRef.value?.ensureLoaded()
}

// Stage 4 的工具：每個都回傳 Promise，轉場完成才結束；除錯按鈕與 AI 的 toolCalls 都走這一組
async function flyToLocation(place: 'institute' | 'taiwan') {
  if (place === 'institute') {
    await cesiumRef.value?.flyToTarget()
    await openBim()
  } else {
    split.value = false
    await sleep(FADE_MS)
    await cesiumRef.value?.flyHome()
  }
}
const tools = {
  flyToLocation,
  async sectionStorey(key: string) { await openBim(); await bimRef.value?.sectionStorey(key) },
  async isolateSpaces(keys: string[]) { await openBim(); await bimRef.value?.isolateSpaces(keys) },
  async highlightSpaces(keys: string[], color?: string) { await openBim(); await bimRef.value?.highlightSpaces(keys, color) },
  async resetView() { await openBim(); await bimRef.value?.resetView() },
}
</script>

<template>
  <div class="stage ui" :class="{ split }">
    <div class="pane pane-map">
      <CesiumView ref="cesiumRef" :active="true" @status="status = $event" />
      <NavPanel @action="cesiumRef?.navigate($event)" />
      <div class="tag">城市</div>
    </div>
    <div class="divider" title="拖曳調整視窗大小" @pointerdown="dragStart"><span /></div>
    <div class="pane pane-bim" :style="{ '--bim-w': bimPct + '%' }">
      <BimView ref="bimRef" :active="split" :model-url="model" @status="status = $event" @pick="pickInfo = $event" />
      <NavPanel @action="bimRef?.navigate($event)" />
      <div class="tag">建築內部（BIM）</div>
      <div class="bim-tools">
        <div class="group">
          <UiSelect v-model="storeyKey" :options="storeyOptions" class="w-28" />
          <Button size="sm" @click="tools.sectionStorey(storeyKey)"><Layers class="size-4" />剖切樓層</Button>
        </div>
        <div class="group">
          <UiSelect v-model="spaceKey" :options="spaceOptions" class="w-56" />
          <Button size="sm" @click="tools.isolateSpaces([spaceKey])"><ScanSearch class="size-4" />隔離</Button>
          <Button size="sm" variant="destructive" @click="tools.highlightSpaces([spaceKey])"><MapPinned class="size-4" />標紅</Button>
        </div>
        <Button size="sm" @click="tools.resetView()"><Eraser class="size-4" />還原</Button>
      </div>
      <div class="bim-info">{{ pickInfo || '點選構件可顯示名稱與類別' }}</div>
      <Button class="close" variant="destructive" size="icon" title="關閉 BIM 視窗" @click="split = false"><X class="size-5" /></Button>
    </div>

    <button class="edge" :title="split ? '收合 BIM 視窗' : '開啟 BIM 視窗'" @click="split = !split">
      <component :is="split ? ChevronRight : ChevronLeft" class="size-5" />
    </button>

    <div class="toolbar">
      <Button variant="default" @click="flyToLocation('institute')"><Plane class="size-4" />飛往大樓</Button>
      <Button @click="flyToLocation('taiwan')"><RotateCcw class="size-4" />回到台灣</Button>
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
  background: #0b1118;
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
  background: #1d3a66;
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
  display: grid;
  place-items: center;
  width: 24px;
  height: 64px;
  cursor: pointer;
  color: #fff;
  background: var(--panel);
  backdrop-filter: blur(8px);
  border: 1px solid var(--line);
  border-right: none;
  border-radius: 10px 0 0 10px;
  transition: background 0.15s;
}
.edge:hover {
  background: rgba(59, 130, 246, 0.85);
}
.close {
  position: absolute;
  top: 12px;
  right: 12px;
  z-index: 10;
}
.bim-tools {
  position: absolute;
  top: 12px;
  left: 12px;
  right: 60px;
  z-index: 10;
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}
.bim-tools .group {
  display: flex;
  gap: 6px;
  padding: 4px;
  border: 1px solid var(--line);
  border-radius: 12px;
  background: var(--panel);
  backdrop-filter: blur(8px);
}
.bim-info {
  position: absolute;
  left: 12px;
  right: 12px;
  bottom: 12px;
  z-index: 10;
  padding: 10px 14px;
  font-size: 15px;
  line-height: 1.5;
  color: #fff;
  background: var(--panel);
  backdrop-filter: blur(8px);
  border: 1px solid var(--line);
  border-radius: 12px;
  pointer-events: none;
  word-break: break-all;
}
.tag {
  position: absolute;
  left: 12px;
  bottom: 84px;
  z-index: 10;
  color: #fff;
  background: var(--panel);
  border: 1px solid var(--line);
  padding: 2px 10px;
  border-radius: 999px;
  font-size: 12px;
  pointer-events: none;
}
.toolbar {
  position: absolute;
  top: 12px;
  left: 12px;
  z-index: 20;
  display: flex;
  gap: 8px;
  align-items: center;
}
.status {
  color: #fff;
  background: var(--panel);
  backdrop-filter: blur(8px);
  border: 1px solid var(--line);
  padding: 4px 12px;
  border-radius: 999px;
  font-size: 12px;
}
</style>
