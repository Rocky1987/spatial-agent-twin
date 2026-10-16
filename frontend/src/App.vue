<script setup lang="ts">
import { ref, watch } from 'vue'
import spaceMap from './data/space-map.json'
import { THRESHOLD, useSensors } from './utils/sensors'
import CesiumView from './components/CesiumView.vue'
import BimView from './components/BimView.vue'
import NavPanel from './components/NavPanel.vue'
import Button from './components/ui/Button.vue'
import UiSelect from './components/ui/UiSelect.vue'
import ChatPanel from './components/ChatPanel.vue'
import { Bot, ChevronLeft, ChevronRight, Eraser, Flame, Layers, MapPinned, Plane, RotateCcw, ScanSearch, TriangleAlert, X } from 'lucide-vue-next'

// 雙視窗：地圖固定在左邊；split 時右邊展開 BIM 窗格
const split = ref(false)
const status = ref('')
const model = ref('/models/AC20-Institute-Var-2.ifc')
const pickInfo = ref('')
const chatOpen = ref(true)
const chatRef = ref<InstanceType<typeof ChatPanel>>()
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

// IoT 模擬：超標的空間自動標紅（只在 BIM 窗格已開時；不會自己把窗格彈出來），告警清單一直顯示在地圖右下角
const { sensors, alerts, spike, connected } = useSensors()
const spaceZh = (k: string) => spaceMap.spaces.find((s) => s.key === k)?.zh ?? k
const alertKeys = () => alerts.value.map((a) => a.spaceKey).sort().join(',')
// 感測器溫度牌：BIM 窗格開著、模型載入後，每次更新都同步到 3D
watch([sensors, split], () => {
  if (split.value) void bimRef.value?.setSensors(sensors.map((s) => ({ spaceKey: s.spaceKey, temp: s.temp, alert: s.temp >= THRESHOLD })))
}, { deep: true })
const barPct = (t: number) => Math.min(100, Math.max(0, ((t - 20) / 20) * 100))
const sensorsOpen = ref(true)
// 自動標紅只上色，不動相機與可見性；要對準告警得由使用者請 AI 執行 showAlerts
watch([alertKeys, split], ([now]) => {
  if (split.value) void bimRef.value?.markSpaces(now ? now.split(',') : [])
})

// 手動按鈕也算一步，這樣「上一步」可以退回按鈕的操作
async function step(name: keyof typeof tools, ...args: unknown[]) {
  const chat = chatRef.value
  chat?.begin()
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  await ((chat ? chat.tracked : tools) as any)[name](...args)
}
</script>

<template>
  <div class="stage ui" :class="{ split }">
    <div class="pane pane-map">
      <CesiumView ref="cesiumRef" :active="true" @status="status = $event" />
      <NavPanel @action="cesiumRef?.navigate($event)" />
      <div class="tag">城市</div>
      <ChatPanel ref="chatRef" v-show="chatOpen" class="chat" :tools="tools" :alerts="alerts" @close="chatOpen = false" />
      <div class="alerts ui">
        <div class="alerts-head">
          <TriangleAlert class="size-4" :class="alerts.length ? 'text-red-400' : 'text-emerald-400'" />
          <span class="flex-1">{{ !connected ? '感測器連線中…' : alerts.length ? `告警 ${alerts.length} 處` : '溫度正常' }}</span>
          <Button size="sm" title="模擬某個空間溫度飆高（錄影與驗收用）" @click="spike()"><Flame class="size-4" />模擬升溫</Button>
        </div>
        <ul v-if="sensorsOpen" class="alerts-list">
          <li
            v-for="s in sensors"
            :key="s.spaceKey"
            :class="{ hot: s.temp >= THRESHOLD }"
            title="點一下：在 BIM 中隔離這個空間"
            @click="step('isolateSpaces', [s.spaceKey])"
          >
            <span class="name">{{ spaceZh(s.spaceKey) }}<small>（{{ s.spaceKey.split('-')[0] }}）</small></span>
            <span class="bar"><i :style="{ width: barPct(s.temp) + '%' }" /><u :style="{ left: barPct(THRESHOLD) + '%' }" /></span>
            <b>{{ s.temp.toFixed(1) }}°</b>
          </li>
        </ul>
        <div class="alerts-foot">
          後端模擬 {{ sensors.length }} 個感測器，經 SignalR 每 2 秒推播；達 {{ THRESHOLD }}°C 就告警並標紅，AI 也看得到。
          <a @click="sensorsOpen = !sensorsOpen">{{ sensorsOpen ? '收合' : '展開' }}</a>
        </div>
      </div>
    </div>
    <div class="divider" title="拖曳調整視窗大小" @pointerdown="dragStart"><span /></div>
    <div class="pane pane-bim" :style="{ '--bim-w': bimPct + '%' }">
      <BimView ref="bimRef" :active="split" :model-url="model" @status="status = $event" @pick="pickInfo = $event" />
      <NavPanel @action="bimRef?.navigate($event)" />
      <div class="tag">建築內部（BIM）</div>
      <div class="bim-tools">
        <div class="group">
          <UiSelect v-model="storeyKey" :options="storeyOptions" class="w-28" />
          <Button size="sm" @click="step('sectionStorey', storeyKey)"><Layers class="size-4" />剖切樓層</Button>
        </div>
        <div class="group">
          <UiSelect v-model="spaceKey" :options="spaceOptions" class="w-56" />
          <Button size="sm" @click="step('isolateSpaces', [spaceKey])"><ScanSearch class="size-4" />隔離</Button>
          <Button size="sm" variant="destructive" @click="step('highlightSpaces', [spaceKey])"><MapPinned class="size-4" />標紅</Button>
        </div>
        <Button size="sm" @click="step('resetView')"><Eraser class="size-4" />還原</Button>
      </div>
      <div class="bim-info">{{ pickInfo || '點選構件可顯示名稱與類別' }}</div>
      <Button class="close" variant="destructive" size="icon" title="關閉 BIM 視窗" @click="split = false"><X class="size-5" /></Button>
    </div>

    <button class="edge" :title="split ? '收合 BIM 視窗' : '開啟 BIM 視窗'" @click="split = !split">
      <component :is="split ? ChevronRight : ChevronLeft" class="size-5" />
    </button>

    <div class="toolbar">
      <Button variant="default" @click="step('flyToLocation', 'institute')"><Plane class="size-4" />飛往大樓</Button>
      <Button @click="step('flyToLocation', 'taiwan')"><RotateCcw class="size-4" />回到台灣</Button>
      <Button v-if="!chatOpen" @click="chatOpen = true"><Bot class="size-4" />AI 助理</Button>
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
.chat {
  position: absolute;
  top: 60px;
  left: 12px;
  z-index: 18;
}
.alerts {
  position: absolute;
  right: 12px;
  bottom: 12px;
  z-index: 10;
  width: 300px;
  max-width: calc(100% - 24px);
  color: #fff;
  font-size: 13px;
  background: var(--panel);
  backdrop-filter: blur(8px);
  border: 1px solid var(--line);
  border-radius: 12px;
  padding: 6px 8px;
}
.alerts-head,
.alerts-list li {
  display: flex;
  align-items: center;
  gap: 6px;
}
.alerts-list {
  margin: 6px 0 0;
  padding: 0;
  list-style: none;
  max-height: 260px;
  overflow-y: auto;
}
.alerts-list li {
  padding: 2px 4px;
  border-radius: 6px;
  cursor: pointer;
}
.alerts-list li:hover {
  background: rgba(255, 255, 255, 0.1);
}
.alerts-list li.hot {
  color: #fecaca;
  background: rgba(220, 38, 38, 0.25);
}
.alerts-list .name {
  flex: 0 0 120px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.alerts-list .bar {
  position: relative;
  flex: 1;
  height: 6px;
  border-radius: 3px;
  background: rgba(255, 255, 255, 0.15);
}
.alerts-list .bar i {
  display: block;
  height: 100%;
  border-radius: 3px;
  background: #22c55e;
  transition: width 0.5s;
}
.alerts-list li.hot .bar i {
  background: #ef4444;
}
.alerts-list .bar u {
  position: absolute;
  top: -3px;
  bottom: -3px;
  width: 1px;
  background: #fff;
}
.alerts-list b {
  flex: 0 0 46px;
  text-align: right;
  font-variant-numeric: tabular-nums;
}
.alerts-foot a {
  margin-left: 4px;
  color: #7dd3fc;
  cursor: pointer;
}
.alerts-list small,
.alerts-foot {
  color: rgba(255, 255, 255, 0.55);
  font-size: 11px;
}
.alerts-foot {
  margin-top: 4px;
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
