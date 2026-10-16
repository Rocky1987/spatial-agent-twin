<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, shallowRef } from 'vue'
import {
  Cartesian2,
  Cartesian3,
  HeadingPitchRange,
  Ion,
  Math as CesiumMath,
  Matrix4,
  OpenStreetMapImageryProvider,
  Viewer,
  WebMapTileServiceImageryProvider,
} from 'cesium'
import 'cesium/Build/Cesium/Widgets/widgets.css'
import type { NavAction } from './NavPanel.vue'

const props = defineProps<{ active: boolean }>()

const container = ref<HTMLDivElement>()
// Viewer 不能被 Vue 深層響應式包住
const viewer = shallowRef<Viewer>()

onMounted(() => {
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
})

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

defineExpose({ viewer, navigate })
void props
</script>

<template>
  <div class="cesium-wrap">
    <div ref="container" class="cesium-container" />
    <div class="credit">圖資來源：內政部國土測繪中心</div>
  </div>
</template>

<style scoped>
.cesium-wrap,
.cesium-container {
  width: 100%;
  height: 100%;
  position: relative;
}
.credit {
  position: absolute;
  left: 8px;
  bottom: 8px;
  padding: 2px 8px;
  font-size: 12px;
  color: #fff;
  background: rgba(0, 0, 0, 0.5);
  border-radius: 4px;
  pointer-events: none;
}
</style>
