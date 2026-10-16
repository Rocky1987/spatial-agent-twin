<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, shallowRef } from 'vue'
import {
  BoundingSphere,
  EllipsoidGeodesic,
  Cartesian2,
  Cartesian3,
  Cartographic,
  Cesium3DTileFeature,
  Cesium3DTileStyle,
  Color,
  createOsmBuildingsAsync,
  HeadingPitchRange,
  Ion,
  Math as CesiumMath,
  Matrix4,
  OpenStreetMapImageryProvider,
  ScreenSpaceEventHandler,
  ScreenSpaceEventType,
  Viewer,
  WebMapTileServiceImageryProvider,
} from 'cesium'
import 'cesium/Build/Cesium/Widgets/widgets.css'
import type { NavAction } from './NavPanel.vue'
import { toTwd97 } from '../utils/twd97'

const props = defineProps<{ active: boolean }>()
const emit = defineEmits<{ (e: 'status', msg: string): void }>()

// 研究所大樓的替身：預設台北 101 一帶；在地圖上點選建物可改選，結果存在 localStorage
type Target = { lon: number; lat: number; elementId?: number | string }
const TARGET_KEY = 'stg.target'
const DEFAULT_TARGET: Target = { lon: 121.5645, lat: 25.034 }
function loadTarget(): Target {
  try {
    const t = JSON.parse(localStorage.getItem(TARGET_KEY) ?? 'null')
    if (t && typeof t.lon === 'number' && typeof t.lat === 'number') return t
  } catch { /* 無法讀取就用預設值 */ }
  return DEFAULT_TARGET
}
let target: Target = loadTarget()
let osm: Awaited<ReturnType<typeof createOsmBuildingsAsync>> | undefined

const container = ref<HTMLDivElement>()
// Viewer 不能被 Vue 深層響應式包住
const viewer = shallowRef<Viewer>()

// 比例尺：量畫面底部中央 100px 對應的地面距離，再取整成好讀的長度
const scale = ref({ px: 0, label: '', ratio: 0 })
// 滑鼠所在位置的座標（顯示在底部資訊列）
const cursor = ref<{ x: number; y: number; lon: number; lat: number } | null>(null)
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
  new ScreenSpaceEventHandler(v.scene.canvas).setInputAction((e: { position: Cartesian2 }) => {
    const picked = v.scene.pick(e.position)
    const pos = v.scene.pickPosition(e.position)
    if (!(picked instanceof Cesium3DTileFeature) || !pos) return
    const c = Cartographic.fromCartesian(pos)
    target = {
      lon: CesiumMath.toDegrees(c.longitude),
      lat: CesiumMath.toDegrees(c.latitude),
      elementId: picked.getProperty('elementId'),
    }
    try { localStorage.setItem(TARGET_KEY, JSON.stringify(target)) } catch { /* 忽略 */ }
    console.info('目標建物', target)
    emit('status', `目標建物：${target.lon.toFixed(5)}, ${target.lat.toFixed(5)}, elementId=${target.elementId}`)
    applyTarget(v)
  }, ScreenSpaceEventType.LEFT_CLICK)

  try {
    osm = await createOsmBuildingsAsync()
    v.scene.primitives.add(osm)
    applyTarget(v)
  } catch (err) {
    console.warn('OSM Buildings 載入失敗，改用標記點', err)
    emit('status', 'OSM Buildings 載入失敗，改用標記點')
  }
})

function applyTarget(v: Viewer) {
  v.entities.removeAll()
  // 一律放標記；有 elementId 且 OSM 載入時，再把建物染色
  v.entities.add({
    position: Cartesian3.fromDegrees(target.lon, target.lat, 0),
    point: { pixelSize: 12, color: Color.RED, outlineColor: Color.WHITE, outlineWidth: 2 },
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
    <div v-if="scale.px" class="scale">
      <span>{{ scale.label }}</span>
      <i :style="{ width: scale.px + 'px' }" />
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
