<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, shallowRef, watch } from 'vue'
import {
  BoundingSphere,
  EllipsoidGeodesic,
  Cartesian2,
  Cartesian3,
  Cartographic,
  Cesium3DTileFeature,
  Cesium3DTileset,
  Cesium3DTileStyle,
  Color,
  createOsmBuildingsAsync,
  HeadingPitchRange,
  Ion,
  Math as CesiumMath,
  Matrix4,
  HeightReference,
  Terrain,
  OpenStreetMapImageryProvider,
  PostProcessStageLibrary,
  ScreenSpaceEventHandler,
  ScreenSpaceEventType,
  Viewer,
  WebMapTileServiceImageryProvider,
} from 'cesium'
import 'cesium/Build/Cesium/Widgets/widgets.css'
import { X } from 'lucide-vue-next'
import Button from './ui/Button.vue'
import type { NavAction } from './NavPanel.vue'
import { toTwd97 } from '../utils/twd97'

const props = defineProps<{ active: boolean }>()
const emit = defineEmits<{ (e: 'status', msg: string): void }>()

// 研究所大樓的替身：預設是 121.62602, 25.03834（elementId 344947612）；在地圖上點選建物、按「設為目標」可改選，結果存在 localStorage
type Target = { lon: number; lat: number; elementId?: number | string }
const TARGET_KEY = 'stg.target'
const DEFAULT_TARGET: Target = { lon: 121.62602, lat: 25.03834, elementId: 344947612 }
function loadTarget(): Target {
  try {
    const t = JSON.parse(localStorage.getItem(TARGET_KEY) ?? 'null')
    if (t && typeof t.lon === 'number' && typeof t.lat === 'number') return t
  } catch { /* 無法讀取就用預設值 */ }
  return DEFAULT_TARGET
}
let target: Target = loadTarget()
let osm: Awaited<ReturnType<typeof createOsmBuildingsAsync>> | undefined

// 圖層清單：OSM Buildings 預設開；內政部三維建物（臺北市，3D Tiles 服務代碼 30，不需 token）預設關，第一次打開才載入
const NLSC_BUILDING_URL = 'https://3dtiles.nlsc.gov.tw/building/tiles3d/30/tileset.json'
const layerState = ref({ osm: true, nlsc: false })
const nlscLoading = ref(false)
let nlsc: Cesium3DTileset | undefined

async function toggleLayer(id: 'osm' | 'nlsc') {
  const v = viewer.value
  if (!v) return
  const on = (layerState.value[id] = !layerState.value[id])
  if (id === 'osm') {
    if (osm) osm.show = on // 還沒載入完的話，載入後依 layerState 決定
  } else if (nlsc) {
    nlsc.show = on
  } else if (on) {
    nlscLoading.value = true
    try {
      nlsc = await Cesium3DTileset.fromUrl(NLSC_BUILDING_URL)
      nlsc.show = layerState.value.nlsc
      v.scene.primitives.add(nlsc)
    } catch (err) {
      console.warn('內政部三維建物載入失敗', err)
      emit('status', '內政部三維建物載入失敗')
      layerState.value.nlsc = false
    } finally {
      nlscLoading.value = false
    }
  }
  v.scene.requestRender()
}

const container = ref<HTMLDivElement>()
// Viewer 不能被 Vue 深層響應式包住
const viewer = shallowRef<Viewer>()

// 比例尺：量畫面底部中央 100px 對應的地面距離，再取整成好讀的長度
const scale = ref({ px: 0, label: '', ratio: 0 })
// 滑鼠所在位置的座標（顯示在底部資訊列）
const cursor = ref<{ x: number; y: number; lon: number; lat: number } | null>(null)
// 點選建物的資訊卡：錨在建物上的世界座標，相機移動時跟著走；按 ✕ 或點空白處才收起
type Popup = { x: number; y: number; world: Cartesian3; rows: [string, string][]; target: Target }
const popup = ref<Popup | null>(null)
// 被選中建物的淡藍外框（後處理輪廓）；popup 收起時清掉
// 多層疊加：由外往內，寬而淡的藍當光暈，窄而亮的青白色當亮邊
let silhouettes: ReturnType<typeof PostProcessStageLibrary.createEdgeDetectionStage>[] = []
function setSelected(f: Cesium3DTileFeature | null) {
  const v = viewer.value
  if (!v) return
  if (!silhouettes.length) {
    const mk = (css: string, length: number) => {
      const st = PostProcessStageLibrary.createEdgeDetectionStage()
      st.uniforms.color = Color.fromCssColorString(css)
      st.uniforms.length = length
      return st
    }
    silhouettes = [
      mk('#0369a1', 0.16),
      mk('#0ea5e9', 0.11),
      mk('#7dd3fc', 0.07),
      mk('#e0faff', 0.035),
    ]
    // 邊緣偵測階段必須包進輪廓合成階段才會只描 selected 的物件
    v.scene.postProcessStages.add(PostProcessStageLibrary.createSilhouetteStage(silhouettes))
  }
  silhouettes.forEach((st) => (st.selected = f ? [f] : []))
  v.scene.requestRender()
}
watch(() => popup.value === null, (closed) => { if (closed) setSelected(null) })

const NICE = [1, 2, 5]
function updateScale(v: Viewer) {
  const cv = v.scene.canvas
  const y = cv.clientHeight - 20
  const x = cv.clientWidth / 2
  const a = v.camera.pickEllipsoid(new Cartesian2(x, y))
  const b = v.camera.pickEllipsoid(new Cartesian2(x + 100, y))
  if (!a || !b) { scale.value = { px: 0, label: '', ratio: 0 }; return }
  const g = new EllipsoidGeodesic(Cartographic.fromCartesian(a), Cartographic.fromCartesian(b))
  const meters100 = g.surfaceDistance
  if (!(meters100 > 0)) return
  const pow = Math.pow(10, Math.floor(Math.log10(meters100)))
  const nice = [...NICE, 10].map((n) => n * pow).filter((m) => m <= meters100).pop() ?? pow
  scale.value = {
    px: Math.round((nice / meters100) * 100),
    label: nice >= 1000 ? `${nice / 1000} km` : `${nice} m`,
    // 比例尺分母：以 OGC 標準像素 0.28 mm 換算
    ratio: Math.round(meters100 / 100 / 0.00028),
  }
}

onMounted(async () => {
  const token = import.meta.env.VITE_CESIUM_ION_TOKEN
  if (token) Ion.defaultAccessToken = token

  const v = new Viewer(container.value!, {
    baseLayer: false,
    // Cesium World Terrain；失敗時維持平面橢球，其餘功能不受影響
    terrain: Terrain.fromWorldTerrain(),
    baseLayerPicker: false,
    geocoder: false,
    homeButton: false,
    sceneModePicker: false,
    navigationHelpButton: false,
    animation: false,
    timeline: false,
    fullscreenButton: false,
    infoBox: false,
    selectionIndicator: false,
    requestRenderMode: true,
    maximumRenderTimeChange: Infinity,
  })

  // 國土測繪中心 WMTS：臺灣通用電子地圖
  const nlsc = new WebMapTileServiceImageryProvider({
    url: 'https://wmts.nlsc.gov.tw/wmts/EMAP/default/{TileMatrixSet}/{TileMatrix}/{TileRow}/{TileCol}',
    layer: 'EMAP',
    style: 'default',
    format: 'image/png',
    tileMatrixSetID: 'GoogleMapsCompatible',
    maximumLevel: 19,
  })
  const layer = v.imageryLayers.addImageryProvider(nlsc)
  // 載不出來就降級成 OSM
  layer.readyEvent.addEventListener(() => {})
  nlsc.errorEvent.addEventListener(() => {
    if (v.imageryLayers.contains(layer)) {
      v.imageryLayers.remove(layer)
      v.imageryLayers.addImageryProvider(new OpenStreetMapImageryProvider({ url: 'https://tile.openstreetmap.org/' }))
      v.scene.requestRender()
    }
  })

  // 初始相機：台灣上空
  resetView(v)
  viewer.value = v
  let raf = 0
  v.scene.canvas.addEventListener('pointermove', (ev) => {
    if (raf) return
    raf = requestAnimationFrame(() => {
      raf = 0
      const r = v.scene.canvas.getBoundingClientRect()
      const p = v.camera.pickEllipsoid(new Cartesian2(ev.clientX - r.left, ev.clientY - r.top))
      if (!p) { cursor.value = null; return }
      const c = Cartographic.fromCartesian(p)
      const lon = CesiumMath.toDegrees(c.longitude)
      const lat = CesiumMath.toDegrees(c.latitude)
      cursor.value = { ...toTwd97(lon, lat), lon, lat }
    })
  })
  v.scene.canvas.addEventListener('pointerleave', () => (cursor.value = null))
  v.scene.postRender.addEventListener(() => updateScale(v))

  // 替身標記（建物高亮失敗時的後備）；點選建物即可改選
  applyTarget(v)
  // 窗格寬度改變時，要求重繪（requestRenderMode 不會自己重畫）
  new ResizeObserver(() => v.scene.requestRender()).observe(container.value!)
  v.scene.postRender.addEventListener(() => {
    const p = popup.value
    if (!p) return
    const w = v.scene.cartesianToCanvasCoordinates(p.world)
    if (w && (w.x !== p.x || w.y !== p.y)) popup.value = { ...p, x: w.x, y: w.y }
  })
  new ScreenSpaceEventHandler(v.scene.canvas).setInputAction((e: { position: Cartesian2 }) => {
    const picked = v.scene.pick(e.position)
    const pos = v.scene.pickPosition(e.position)
    if (!(picked instanceof Cesium3DTileFeature) || !pos) { popup.value = null; return }
    const c = Cartographic.fromCartesian(pos)
    const lon = CesiumMath.toDegrees(c.longitude)
    const lat = CesiumMath.toDegrees(c.latitude)
    const prop = (k: string) => {
      const val = picked.getProperty(k)
      return val === undefined || val === null || val === '' ? undefined : String(val)
    }
    const addr = [prop('addr:city'), prop('addr:street'), prop('addr:housenumber')].filter(Boolean).join(' ')
    const h = prop('height') ?? prop('cesium#estimatedHeight')
    const rows: [string, string][] = [
      ['名稱', prop('name') ?? '（OSM 未命名）'],
      ['類型', prop('building') ?? '—'],
      ['高度', h ? `${Number(h).toFixed(1)} m` : '—'],
      ['地址', addr || '—'],
      ['elementId', prop('elementId') ?? '—'],
      ['經緯度', `${lon.toFixed(5)}, ${lat.toFixed(5)}`],
      ['TWD97', (({ x, y }) => `${Math.round(x)}, ${Math.round(y)}`)(toTwd97(lon, lat))],
    ]
    setSelected(picked)
    popup.value = { x: e.position.x, y: e.position.y, world: pos, rows, target: { lon, lat, elementId: picked.getProperty('elementId') } }
  }, ScreenSpaceEventType.LEFT_CLICK)

  try {
    osm = await createOsmBuildingsAsync()
    osm.show = layerState.value.osm
    v.scene.primitives.add(osm)
    applyTarget(v)
  } catch (err) {
    console.warn('OSM Buildings 載入失敗，改用標記點', err)
    emit('status', 'OSM Buildings 載入失敗，改用標記點')
  }
})

// 資訊卡放在錨點右側；右側放不下就改放左側，並用箭頭指向錨點
const CARD_W = 256
const CARD_H = 290
const cardSide = computed(() => {
  const p = popup.value
  const w = container.value?.clientWidth ?? 800
  return p && p.x + 20 + CARD_W > w ? 'right' : 'left'
})
const cardTop = computed(() => {
  const p = popup.value
  const h = container.value?.clientHeight ?? 600
  return p ? Math.min(Math.max(8, p.y - 40), Math.max(8, h - CARD_H)) : 0
})
const arrowTop = computed(() => Math.min(CARD_H - 40, Math.max(14, (popup.value?.y ?? 0) - cardTop.value - 8)))
const cardStyle = computed(() => {
  const p = popup.value
  if (!p) return {}
  const left = cardSide.value === 'left' ? p.x + 16 : p.x - 16 - CARD_W
  return { left: left + 'px', top: cardTop.value + 'px' }
})

function setTargetFromPopup() {
  const v = viewer.value
  if (!v || !popup.value) return
  target = popup.value.target
  try { localStorage.setItem(TARGET_KEY, JSON.stringify(target)) } catch { /* 忽略 */ }
  console.info('目標建物', target)
  emit('status', `目標建物：${target.lon.toFixed(5)}, ${target.lat.toFixed(5)}, elementId=${target.elementId}`)
  popup.value = null
  applyTarget(v)
}

function applyTarget(v: Viewer) {
  v.entities.removeAll()
  // 一律放標記；有 elementId 且 OSM 載入時，再把建物染色
  v.entities.add({
    position: Cartesian3.fromDegrees(target.lon, target.lat, 0),
    point: { heightReference: HeightReference.CLAMP_TO_GROUND, pixelSize: 12, color: Color.RED, outlineColor: Color.WHITE, outlineWidth: 2 },
  })
  if (osm) {
    osm.style = new Cesium3DTileStyle({
      color: {
        conditions: [
          ...(target.elementId !== undefined ? [[`\${elementId} === ${JSON.stringify(target.elementId)}`, "color('#ff3b30')"]] : []),
          ['true', "color('white')"],
        ],
      },
    })
  }
  v.scene.requestRender()
}

function flyDone(fn: (done: () => void) => void): Promise<void> {
  return new Promise((resolve) => fn(resolve))
}

// 飛到大樓上空斜視；飛完才 resolve
function flyToTarget(): Promise<void> {
  const v = viewer.value
  if (!v) return Promise.resolve()
  const sphere = new BoundingSphere(Cartesian3.fromDegrees(target.lon, target.lat, 20), 1)
  return flyDone((done) =>
    v.camera.flyToBoundingSphere(sphere, {
      offset: new HeadingPitchRange(CesiumMath.toRadians(30), CesiumMath.toRadians(-35), 350),
      duration: 3,
      complete: done,
      cancel: done,
    }),
  )
}

// 相機拉回台灣全景
function flyHome(): Promise<void> {
  const v = viewer.value
  if (!v) return Promise.resolve()
  return flyDone((done) =>
    v.camera.flyTo({
      destination: Cartesian3.fromDegrees(121.0, 23.7, 450000),
      duration: 2.5,
      complete: done,
      cancel: done,
    }),
  )
}

function resetView(v: Viewer) {
  v.camera.setView({ destination: Cartesian3.fromDegrees(121.0, 23.7, 450000) })
  v.scene.requestRender()
}

// 繞著畫面中心點旋轉／傾斜（Cesium 慣用的 lookAt 環繞做法）
function orbit(v: Viewer, dHeading: number, dPitch: number) {
  const cam = v.camera
  const canvas = v.scene.canvas
  const center = cam.pickEllipsoid(new Cartesian2(canvas.clientWidth / 2, canvas.clientHeight / 2))
  if (!center) return
  const range = Cartesian3.distance(cam.position, center)
  const pitch = Math.min(CesiumMath.toRadians(-5), Math.max(CesiumMath.toRadians(-89), cam.pitch + dPitch))
  cam.lookAt(center, new HeadingPitchRange(cam.heading + dHeading, pitch, range))
  cam.lookAtTransform(Matrix4.IDENTITY)
  v.scene.requestRender()
}

function navigate(a: NavAction) {
  const v = viewer.value
  if (!v) return
  const step = CesiumMath.toRadians(15)
  const h = v.camera.positionCartographic.height
  switch (a) {
    case 'zoomIn': v.camera.zoomIn(h * 0.3); break
    case 'zoomOut': v.camera.zoomOut(h * 0.4); break
    case 'rotateLeft': orbit(v, -step, 0); break
    case 'rotateRight': orbit(v, step, 0); break
    case 'tiltUp': orbit(v, 0, -step); break
    case 'tiltDown': orbit(v, 0, step); break
    case 'reset': resetView(v); break
  }
  v.scene.requestRender()
}

onBeforeUnmount(() => viewer.value?.destroy())

defineExpose({ viewer, navigate, flyToTarget, flyHome })
void props
</script>

<template>
  <div class="cesium-wrap">
    <div ref="container" class="cesium-container" />
    <div class="ui absolute right-[64px] top-[60px] z-10 rounded-xl border border-solid border-[var(--line)] bg-[var(--panel)] px-3 py-2 text-sm text-white backdrop-blur">
      <div class="mb-1 font-semibold">圖層</div>
      <label class="flex cursor-pointer items-center gap-2 py-0.5">
        <input type="checkbox" :checked="layerState.osm" @change="toggleLayer('osm')" />
        OSM 建物
      </label>
      <label class="flex cursor-pointer items-center gap-2 py-0.5">
        <input type="checkbox" :checked="layerState.nlsc" :disabled="nlscLoading" @change="toggleLayer('nlsc')" />
        內政部三維建物（臺北市）
        <span v-if="nlscLoading" class="opacity-60">載入中…</span>
      </label>
    </div>
    <div v-if="scale.px" class="scale">
      <span>{{ scale.label }}</span>
      <i :style="{ width: scale.px + 'px' }" />
    </div>
    <div
      v-if="popup"
      class="ui absolute z-20 w-64 rounded-xl border border-solid border-[var(--line)] bg-[var(--panel-solid)]/95 p-3 text-sm text-white shadow-xl backdrop-blur"
      :style="cardStyle"
    >
      <i class="arrow" :class="cardSide" :style="{ top: arrowTop + 'px' }" />
      <div class="mb-2 flex items-center justify-between font-semibold">
        建物資訊
        <Button variant="ghost" size="icon" class="-mr-2 -mt-1 size-7" @click="popup = null"><X class="size-4" /></Button>
      </div>
      <dl class="m-0 grid grid-cols-[auto_1fr] gap-x-3 gap-y-1">
        <template v-for="[k, val] in popup.rows" :key="k">
          <dt class="opacity-60">{{ k }}</dt>
          <dd class="m-0 break-all">{{ val }}</dd>
        </template>
      </dl>
      <Button variant="default" size="sm" class="mt-3 w-full" @click="setTargetFromPopup">設為目標建物</Button>
    </div>
    <div class="credit">
      <span>圖資來源：內政部國土測繪中心 ©OpenStreetMap contributors</span>
      <span v-if="scale.ratio">比例尺：{{ scale.ratio.toLocaleString() }}</span>
      <span v-if="cursor">
        TWD97 坐標：{{ Math.round(cursor.x) }},{{ Math.round(cursor.y) }}　經緯度：{{ cursor.lon.toFixed(5) }}, {{ cursor.lat.toFixed(5) }}
      </span>
    </div>
  </div>
</template>

<style scoped>
.arrow {
  position: absolute;
  width: 14px;
  height: 14px;
  background: var(--panel-solid);
  border: 1px solid var(--line);
  transform: rotate(45deg);
}
.arrow.left {
  left: -8px;
  border-right: none;
  border-top: none;
}
.arrow.right {
  right: -8px;
  border-left: none;
  border-bottom: none;
}
.cesium-wrap,
.cesium-container {
  width: 100%;
  height: 100%;
  position: relative;
}
.scale {
  position: absolute;
  left: 8px;
  bottom: 34px;
  padding: 4px 8px 6px;
  color: #fff;
  font-size: 13px;
  font-weight: 600;
  background: rgba(0, 0, 0, 0.6);
  border-radius: 4px;
  pointer-events: none;
}
.scale i {
  display: block;
  height: 7px;
  margin-top: 3px;
  border: 2px solid #fff;
  border-top: none;
}
.credit {
  position: absolute;
  left: 0;
  right: 0;
  bottom: 0;
  display: flex;
  flex-wrap: wrap;
  gap: 4px 16px;
  padding: 3px 10px;
  font-size: 12px;
  color: #fff;
  background: rgba(0, 0, 0, 0.7);
  pointer-events: none;
}
</style>
