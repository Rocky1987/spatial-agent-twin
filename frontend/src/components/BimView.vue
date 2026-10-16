<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, shallowRef, watch } from 'vue'
import * as OBC from '@thatopen/components'
import * as THREE from 'three'
import type { NavAction } from './NavPanel.vue'

const props = defineProps<{ active: boolean; modelUrl: string }>()
const emit = defineEmits<{ (e: 'status', msg: string): void }>()

const container = ref<HTMLDivElement>()
// That Open 物件都放非響應式
const components = shallowRef<OBC.Components>()
let fitBox: THREE.Box3 | undefined
const world = shallowRef<OBC.SimpleWorld<OBC.SimpleScene, OBC.OrthoPerspectiveCamera, OBC.SimpleRenderer>>()

async function loadIfc(url: string, name: string) {
  const comps = components.value!
  const t0 = performance.now()
  emit('status', `載入 ${name} …`)
  const buffer = new Uint8Array(await (await fetch(url)).arrayBuffer())
  await comps.get(OBC.IfcLoader).load(buffer, false, name)
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
  try {
    await loadIfc(props.modelUrl, 'model')
    fitBox = new THREE.Box3()
    for (const [, m] of fragments.list) fitBox.union(m.box)
    await w.camera.controls.fitToBox(fitBox, false)
  } catch (e) {
    emit('status', `IFC 載入失敗：${e}`)
    console.error(e)
  }
  setPaused(!props.active)
})

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
  if (a) components.value?.get(OBC.FragmentsManager).core.update(true)
})

onBeforeUnmount(() => components.value?.dispose())

defineExpose({ components, world, loadIfc, navigate })
</script>

<template>
  <div ref="container" class="bim-container" />
</template>

<style scoped>
.bim-container {
  width: 100%;
  height: 100%;
  position: relative;
  background: linear-gradient(#1b2a3a, #0d141c);
}
</style>
