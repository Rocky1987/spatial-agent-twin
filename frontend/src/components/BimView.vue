<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, shallowRef, watch } from 'vue'
import * as OBC from '@thatopen/components'
import * as FRAGS from '@thatopen/fragments'
import * as THREE from 'three'
import spaceMap from '../data/space-map.json'
import type { NavAction } from './NavPanel.vue'

const props = defineProps<{ active: boolean; modelUrl: string }>()
const emit = defineEmits<{
  (e: 'status', msg: string): void
  (e: 'pick', msg: string): void
}>()

const container = ref<HTMLDivElement>()
// That Open 物件都放非響應式
const components = shallowRef<OBC.Components>()
let fitBox: THREE.Box3 | undefined
const world = shallowRef<OBC.SimpleWorld<OBC.SimpleScene, OBC.OrthoPerspectiveCamera, OBC.SimpleRenderer>>()
let ro: ResizeObserver | undefined

// 載入進度（百分比與已耗秒數），顯示在窗格中央
const loading = ref<{ label: string; pct: number | null; sec: number } | null>(null)
let loadTimer: ReturnType<typeof setInterval> | undefined
function startLoading(label: string) {
  const t0 = performance.now()
  loading.value = { label, pct: null, sec: 0 }
  loadTimer = setInterval(() => {
    if (loading.value) loading.value.sec = (performance.now() - t0) / 1000
  }, 100)
}
function stopLoading() {
  clearInterval(loadTimer)
  loading.value = null
}

async function loadIfc(url: string, name: string) {
  const comps = components.value!
  const t0 = performance.now()
  emit('status', `載入 ${name} …`)
  const buffer = new Uint8Array(await (await fetch(url)).arrayBuffer())
  await comps.get(OBC.IfcLoader).load(buffer, false, name, {
    processData: {
      progressCallback: (p) => {
        if (loading.value) loading.value = { ...loading.value, label: '解析 IFC 模型', pct: Math.round(p * 100) }
      },
    },
  })
  emit('status', `${name} 載入完成，${((performance.now() - t0) / 1000).toFixed(1)} 秒`)
}

onMounted(async () => {
  const comps = new OBC.Components()
  const w = comps.get(OBC.Worlds).create<OBC.SimpleScene, OBC.OrthoPerspectiveCamera, OBC.SimpleRenderer>()
  w.scene = new OBC.SimpleScene(comps)
  w.scene.setup()
  w.scene.three.background = null
  w.renderer = new OBC.SimpleRenderer(comps, container.value!)
  w.camera = new OBC.OrthoPerspectiveCamera(comps)
  comps.init()

  const fragments = comps.get(OBC.FragmentsManager)
  fragments.init('/wasm/fragments-worker.mjs')
  w.camera.controls.addEventListener('update', () => fragments.core.update())
  w.onCameraChanged.add((camera) => {
    for (const [, model] of fragments.list) model.useCamera(camera.three)
  })
  fragments.list.onItemSet.add(({ value: model }) => {
    model.useCamera(w.camera.three)
    w.scene.three.add(model.object)
    fragments.core.update(true)
  })

  await comps.get(OBC.IfcLoader).setup({
    autoSetWasm: false,
    wasm: { path: '/wasm/', absolute: true },
  })

  components.value = comps
  world.value = w
  setPaused(!props.active)
  // 模型改成第一次展開窗格時才載入（Institute 有 10.9 MB，不拖慢首頁）
  if (props.active) void ensureLoaded()

  // 點選構件：顯示名稱與類別（除錯用）；拖曳旋轉時不算點選
  const el = container.value!
  let down: { x: number; y: number } | undefined
  el.addEventListener('pointerdown', (e) => (down = { x: e.clientX, y: e.clientY }))
  el.addEventListener('pointerup', (e) => {
    if (down && Math.hypot(e.clientX - down.x, e.clientY - down.y) < 4) void pickAt(e.clientX, e.clientY)
    down = undefined
  })

  // 視窗寬度改變（分割／收合）時，同步渲染器與相機比例
  ro = new ResizeObserver(() => syncSize())
  ro.observe(el)
})

function syncSize() {
  const w = world.value
  const el = container.value
  if (!w || !el || el.clientWidth === 0) return
  w.renderer?.resize()
  w.camera.updateAspect()
}

// ---- Stage 3：模型索引與互動函式 ----
type StoreyInfo = { localId: number; items: number[]; spaces: number[]; floorY: number }
let model: FRAGS.FragmentsModel | undefined
let loadPromise: Promise<void> | undefined
const storeyInfo = new Map<string, StoreyInfo>() // 樓層 key → 資料
const spaceLocal = new Map<string, number>() // 空間 key → localId
let clipPlane: THREE.Plane | undefined

const storeyKeys = spaceMap.storeys.map((s) => s.key) // 已依標高由低到高排序

function findNode(n: FRAGS.SpatialTreeItem, id: number): FRAGS.SpatialTreeItem | undefined {
  if (n.localId === id) return n
  for (const c of n.children ?? []) {
    const r = findNode(c, id)
    if (r) return r
  }
}

function collect(n: FRAGS.SpatialTreeItem, out: { all: number[]; spaces: number[] }) {
  if (n.localId !== null) {
    out.all.push(n.localId)
    if (n.category && /IFCSPACE/i.test(n.category)) out.spaces.push(n.localId)
  }
  for (const c of n.children ?? []) collect(c, out)
}

async function buildIndex(m: FRAGS.FragmentsModel) {
  const guids = [...spaceMap.storeys, ...spaceMap.spaces].map((x) => x.globalId)
  const ids = await m.getLocalIdsByGuids(guids)
  const byGuid = new Map(guids.map((g, i) => [g, ids[i]]))
  const tree = await m.getSpatialStructure()
  for (const s of spaceMap.storeys) {
    const localId = byGuid.get(s.globalId)
    if (localId == null) { console.warn('找不到樓層', s.key); continue }
    const node = findNode(tree, localId)
    const out = { all: [] as number[], spaces: [] as number[] }
    if (node) collect(node, out)
    const children = await m.getItemsChildren([localId])
    const items = [...new Set([...out.all, ...children])]
    // 空間不在樓層的空間結構樹裡，改用對照表的所屬樓層
    const spaces = spaceMap.spaces.filter((sp) => sp.storey === s.key).map((sp) => byGuid.get(sp.globalId)).filter((x): x is number => x != null)
    storeyInfo.set(s.key, { localId, items, spaces, floorY: s.elevation })
  }
  // 地板高度：有收錄空間的樓層用空間最低點，其餘樓層以相同偏移量由標高推算
  const offsets: number[] = []
  for (const s of spaceMap.storeys) {
    const info = storeyInfo.get(s.key)
    if (info?.spaces.length) offsets.push((await m.getMergedBox(info.spaces)).min.y - s.elevation)
  }
  const offset = offsets.length ? offsets.reduce((a, b) => a + b, 0) / offsets.length : 0
  for (const s of spaceMap.storeys) {
    const info = storeyInfo.get(s.key)
    if (!info) continue
    info.floorY = s.elevation + offset
    console.info(`樓層 ${s.key}：IFC 標高 ${s.elevation}，地板 Y=${info.floorY.toFixed(2)}，構件 ${info.items.length}，收錄空間 ${info.spaces.length}`)
  }
  for (const sp of spaceMap.spaces) {
    const id = byGuid.get(sp.globalId)
    if (id == null) console.warn('找不到空間', sp.key)
    else spaceLocal.set(sp.key, id)
  }
}

async function loadAll() {
  syncSize()
  startLoading('下載 IFC 檔案')
  try {
    await loadAndIndex()
  } finally {
    stopLoading()
  }
}

async function loadAndIndex() {
  await loadIfc(props.modelUrl, 'model')
  const fragments = components.value!.get(OBC.FragmentsManager)
  model = [...fragments.list.values()][0]
  fitBox = new THREE.Box3()
  for (const [, m] of fragments.list) fitBox.union(m.box)
  syncSize()
  await world.value!.camera.controls.fitToBox(fitBox, false)
  if (loading.value) loading.value = { ...loading.value, label: '建立樓層與空間索引', pct: null }
  await buildIndex(model)
  emit('status', '模型與對照表索引完成')
}

// 第一次展開窗格時才載入；之後都回傳同一個 Promise
function ensureLoaded(): Promise<void> {
  loadPromise ??= loadAll().catch((e) => {
    emit('status', `IFC 載入失敗：${e}`)
    console.error(e)
    loadPromise = undefined
  })
  return loadPromise
}

// key 或 globalId → localId
function resolveSpaces(refs: string[]): number[] {
  const ids: number[] = []
  for (const r of refs) {
    const sp = spaceMap.spaces.find((x) => x.key === r || x.globalId === r)
    const id = sp && spaceLocal.get(sp.key)
    if (id == null) console.warn('對照表找不到空間', r)
    else ids.push(id)
  }
  return ids
}

function refresh() {
  components.value?.get(OBC.FragmentsManager).core.update(true)
}

function clearSection() {
  const w = world.value
  if (clipPlane && w?.renderer) {
    w.renderer.setPlane(false, clipPlane)
    model?.useClippingPlanes(w.renderer.clippingPlanes)
    clipPlane = undefined
  }
}

async function resetView() {
  await ensureLoaded()
  if (!model) return
  clearSection()
  await model.resetVisible()
  await model.resetHighlight()
  pickedId = null
  if (fitBox) await world.value?.camera.controls.fitToBox(fitBox, true)
  refresh()
}

// 相機對準並改成俯視角：polar 0 = 正上方。留一點傾斜，才看得出立體與牆面
const PLAN_POLAR = THREE.MathUtils.degToRad(25)
const OBLIQUE_POLAR = THREE.MathUtils.degToRad(40)
async function frame(box: THREE.Box3, polar: number) {
  const c = world.value?.camera.controls
  if (!c) return
  await Promise.all([c.rotatePolarTo(polar, true), c.fitToBox(box, true)])
}

// 隱藏上方樓層，並在該層地板上方 1.2 m 剖切（平面圖慣例）
async function sectionStorey(key: string) {
  await ensureLoaded()
  const w = world.value
  const info = storeyInfo.get(key)
  if (!model || !w || !info) { console.warn('找不到樓層', key); return }
  await resetView()
  const above = storeyKeys.slice(storeyKeys.indexOf(key) + 1).flatMap((k) => storeyInfo.get(k)?.items ?? [])
  if (above.length) await model.setVisible(above, false)
  const y = info.floorY + 1.2
  clipPlane = new THREE.Plane(new THREE.Vector3(0, -1, 0), y) // 保留 y 以下
  w.renderer!.setPlane(true, clipPlane)
  model.useClippingPlanes(w.renderer!.clippingPlanes)
  const box = fitBox!.clone()
  box.max.y = y
  await frame(box, PLAN_POLAR)
  refresh()
}

function paint(ids: number[], color: string) {
  return model!.highlight(ids, {
    color: new THREE.Color(color),
    renderedFaces: FRAGS.RenderedFaces.TWO,
    opacity: 0.6,
    transparent: true,
  })
}

// 只顯示這些空間所在的樓層，空間上色，相機對準
async function isolateSpaces(refs: string[], color = '#ff3b30') {
  await ensureLoaded()
  const w = world.value
  if (!model || !w) return
  const ids = resolveSpaces(refs)
  if (!ids.length) return
  await resetView()
  const keep = new Set<number>()
  for (const [, info] of storeyInfo) {
    if (ids.some((i) => info.spaces.includes(i))) info.items.forEach((i) => keep.add(i))
  }
  ids.forEach((i) => keep.add(i))
  if (!keep.size) {
    console.warn('隔離：沒有可顯示的構件')
    return
  }
  await model.setVisible(undefined, false)
  await model.setVisible([...keep], true)
  await paint(ids, color)
  const box = await model.getMergedBox(ids)
  // 再在房間頂部剖一刀：樓上的樓板與屋頂即使被重新載入也不會蓋住視野（滾輪縮放時會發生）
  clipPlane = new THREE.Plane(new THREE.Vector3(0, -1, 0), box.max.y + 0.2)
  w.renderer!.setPlane(true, clipPlane)
  model.useClippingPlanes(w.renderer!.clippingPlanes)
  await frame(box.expandByScalar(4), OBLIQUE_POLAR)
  refresh()
}

// 只上色，不改變可見性（Stage 5 的告警會用）
async function highlightSpaces(refs: string[], color = '#ff3b30') {
  await ensureLoaded()
  if (!model) return
  const ids = resolveSpaces(refs)
  await model.resetHighlight() // 每次標紅都取代上一次的標示
  if (ids.length) {
    await paint(ids, color)
    await frame((await model.getMergedBox(ids)).expandByScalar(4), OBLIQUE_POLAR)
  }
  refresh()
}

// ---- Stage 5：感測器標籤（飄在各空間中央的溫度牌，穿牆可見） ----
const sprites = new Map<string, { sprite: THREE.Sprite; ctx: CanvasRenderingContext2D; tex: THREE.CanvasTexture }>()
let spritesInit: Promise<void> | undefined
// 背景：渲染器是透明的，底色由 CSS 漸層決定；預設淺色天空，深色模型（或想要夜間感）可切回深色
const bgLight = ref(true)
// 圖層開關：IoT 溫度牌預設顯示；關掉只是隱藏，資料與告警標紅照常更新
const iotOn = ref(true)
function toggleIot() {
  iotOn.value = !iotOn.value
  for (const { sprite } of sprites.values()) sprite.visible = iotOn.value
  refresh()
}
async function initSprites(keys: string[]) {
  for (const key of keys) {
    const id = spaceLocal.get(key)
    if (id == null) continue
    const box = await model!.getMergedBox([id])
    const canvas = document.createElement('canvas')
    canvas.width = 160
    canvas.height = 80
    const tex = new THREE.CanvasTexture(canvas)
    tex.colorSpace = THREE.SRGBColorSpace
    const sprite = new THREE.Sprite(new THREE.SpriteMaterial({ map: tex, depthTest: false, transparent: true }))
    sprite.renderOrder = 999
    sprite.visible = iotOn.value
    sprite.scale.set(5, 2.5, 1)
    sprite.position.set((box.min.x + box.max.x) / 2, box.min.y + (box.max.y - box.min.y) * 0.5, (box.min.z + box.max.z) / 2)
    world.value!.scene.three.add(sprite)
    sprites.set(key, { sprite, ctx: canvas.getContext('2d')!, tex })
  }
}

// 每次感測器更新就重畫牌子：正常綠色、超標紅色
async function setSensors(list: { spaceKey: string; temp: number; alert: boolean }[]) {
  if (!model || !world.value || !spaceLocal.size) return
  spritesInit ??= initSprites(list.map((s) => s.spaceKey))
  await spritesInit
  for (const s of list) {
    const e = sprites.get(s.spaceKey)
    if (!e) continue
    const { ctx, tex } = e
    ctx.clearRect(0, 0, 160, 80)
    ctx.fillStyle = s.alert ? '#dc2626' : '#15803d'
    ctx.beginPath()
    ctx.roundRect(4, 4, 152, 72, 16)
    ctx.fill()
    ctx.lineWidth = 4
    ctx.strokeStyle = '#fff'
    ctx.stroke()
    ctx.fillStyle = '#fff'
    ctx.font = 'bold 40px sans-serif'
    ctx.textAlign = 'center'
    ctx.textBaseline = 'middle'
    ctx.fillText(`${s.temp.toFixed(1)}°`, 80, 42)
    tex.needsUpdate = true
  }
}

// 只上色：不隱藏、不剖切、不動相機（告警自動標紅用，避免打斷使用者正在看的畫面）
async function markSpaces(refs: string[]) {
  if (!model) return
  const ids = resolveSpaces(refs)
  await model.resetHighlight()
  if (ids.length) await paint(ids, '#ff3b30')
  refresh()
}

const CAT_ZH: Record<string, string> = {
  IFCWALL: '牆', IFCWALLSTANDARDCASE: '牆', IFCSLAB: '樓板', IFCWINDOW: '窗', IFCDOOR: '門',
  IFCSTAIR: '樓梯', IFCSTAIRFLIGHT: '梯段', IFCROOF: '屋頂', IFCCOLUMN: '柱', IFCBEAM: '梁',
  IFCSPACE: '空間', IFCBUILDINGSTOREY: '樓層', IFCRAILING: '欄杆', IFCCOVERING: '覆蓋層（天花板／地板面層）',
  IFCFURNISHINGELEMENT: '家具', IFCOPENINGELEMENT: '開口', IFCMEMBER: '構件', IFCPLATE: '板',
  IFCBUILDINGELEMENTPROXY: '通用建築構件', IFCFLOWTERMINAL: '末端設備', IFCDISTRIBUTIONELEMENT: '機電設備',
  IFCCURTAINWALL: '帷幕牆', IFCFOOTING: '基礎', IFCRAMP: '坡道', IFCSITE: '基地', IFCBUILDING: '建築物',
}

let pickedId: number | null = null

async function pickAt(x: number, y: number) {
  const w = world.value
  if (!model || !w?.renderer) return
  const hit = await model.raycast({
    camera: w.camera.three,
    mouse: new THREE.Vector2(x, y),
    dom: w.renderer.three.domElement,
  })
  if (pickedId !== null) await model.resetHighlight([pickedId])
  pickedId = null
  if (!hit) { refresh(); return }
  pickedId = hit.localId
  await model.highlight([hit.localId], {
    color: new THREE.Color('#ffd400'),
    renderedFaces: FRAGS.RenderedFaces.TWO,
    opacity: 0.8,
    transparent: true,
  })
  refresh()
  const [d] = await model.getItemsData([hit.localId], {
    attributesDefault: true,
    relationsDefault: { attributes: false, relations: false },
  })
  const attr = (k: string) => (d?.[k] as FRAGS.ItemAttribute | undefined)?.value
  const cat = String(attr('_category') ?? '?')
  const guid = String(attr('_guid') ?? '')
  const sp = spaceMap.spaces.find((x) => x.globalId === guid)
  const catZh = CAT_ZH[cat.toUpperCase()] ?? '其他'
  const name = sp ? `${sp.zh}（${sp.longName}）` : (attr('Name') ?? '（無名稱）')
  emit('pick', `類別：${catZh}（${cat}）　名稱：${name}　GUID：${guid}`)
}

function navigate(a: NavAction) {
  const c = world.value?.camera.controls
  if (!c) return
  const step = THREE.MathUtils.degToRad(15)
  switch (a) {
    case 'zoomIn': c.dollyTo(c.distance * 0.7, true); break
    case 'zoomOut': c.dollyTo(c.distance / 0.7, true); break
    case 'rotateLeft': c.rotate(-step, 0, true); break
    case 'rotateRight': c.rotate(step, 0, true); break
    case 'tiltUp': c.rotate(0, -step, true); break
    case 'tiltDown': c.rotate(0, step, true); break
    case 'reset': if (fitBox) c.fitToBox(fitBox, true); break
  }
}

function setPaused(paused: boolean) {
  const w = world.value
  if (w?.renderer) w.renderer.enabled = !paused
  if (w?.camera) w.camera.enabled = !paused
}

watch(() => props.active, (a) => {
  setPaused(!a)
  if (a) {
    syncSize()
    if (!loadPromise) { void ensureLoaded(); return }
    refresh()
  }
})

onBeforeUnmount(() => {
  ro?.disconnect()
  components.value?.dispose()
})

defineExpose({
  components, world, loadIfc, navigate,
  ensureLoaded, sectionStorey, isolateSpaces, highlightSpaces, resetView, setSensors, markSpaces,
})
</script>

<template>
  <div class="bim-wrap">
    <div ref="container" class="bim-container" :class="{ light: bgLight }" />
    <div class="ui absolute right-[64px] top-[60px] z-10 rounded-xl border border-solid border-[var(--line)] bg-[var(--panel)] px-3 py-2 text-sm text-white backdrop-blur">
      <div class="mb-1 font-semibold">圖層</div>
      <label class="flex cursor-pointer items-center gap-2 py-0.5">
        <input v-model="bgLight" type="checkbox" />
        淺色背景
      </label>
      <label class="flex cursor-pointer items-center gap-2 py-0.5">
        <input type="checkbox" :checked="iotOn" @change="toggleIot" />
        IoT 溫度
      </label>
    </div>
    <div v-if="loading" class="loading">
      <div class="loading-label">{{ loading.label }}</div>
      <div class="bar"><i :class="{ indet: loading.pct === null }" :style="loading.pct === null ? undefined : { width: loading.pct + '%' }" /></div>
      <div class="loading-meta">
        <span v-if="loading.pct !== null">{{ loading.pct }}%　</span>已耗時 {{ loading.sec.toFixed(1) }} 秒
      </div>
    </div>
  </div>
</template>

<style scoped>
.bim-wrap {
  position: relative;
  width: 100%;
  height: 100%;
}
.loading {
  position: absolute;
  inset: 0;
  z-index: 5;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 12px;
  color: #fff;
  background: rgba(10, 16, 24, 0.7);
  backdrop-filter: blur(6px);
  pointer-events: none;
}
.loading-label {
  font-size: 16px;
  font-weight: 600;
}
.bar {
  width: min(320px, 70%);
  height: 8px;
  overflow: hidden;
  border-radius: 4px;
  background: rgba(255, 255, 255, 0.18);
}
.bar i {
  display: block;
  height: 100%;
  background: linear-gradient(90deg, #3b82f6, #60a5fa);
  border-radius: 4px;
  transition: width 0.2s;
}
.bar i.indet {
  width: 35%;
  animation: slide 1.1s ease-in-out infinite alternate;
}
@keyframes slide {
  from { transform: translateX(0); }
  to { transform: translateX(185%); }
}
.loading-meta {
  font-size: 14px;
}
.bim-container {
  width: 100%;
  height: 100%;
  position: relative;
  background: linear-gradient(#1b2a3a, #0d141c);
}
/* 淺色：上方天空藍、下方偏暖的地面灰，建物（白灰）與紅色標記都看得清楚 */
.bim-container.light {
  background: linear-gradient(#cfe3f5 0%, #e9eff5 55%, #c9ccd0 100%);
}
</style>
