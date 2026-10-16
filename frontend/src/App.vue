<script setup lang="ts">
import { ref } from 'vue'
import CesiumView from './components/CesiumView.vue'
import BimView from './components/BimView.vue'
import NavPanel, { type NavAction } from './components/NavPanel.vue'

type Mode = 'city' | 'bim'
const mode = ref<Mode>('city')
const status = ref('')
const model = ref('/models/AC20-FZK-Haus.ifc')

const cesiumRef = ref<InstanceType<typeof CesiumView>>()
const bimRef = ref<InstanceType<typeof BimView>>()

// 按鈕只作用在目前顯示的畫面
function onNav(a: NavAction) {
  if (mode.value === 'city') cesiumRef.value?.navigate(a)
  else bimRef.value?.navigate(a)
}

function toggle() {
  mode.value = mode.value === 'city' ? 'bim' : 'city'
}
</script>

<template>
  <div class="stage">
    <!-- 兩個畫面都保持掛載，只改 opacity 與 pointer-events -->
    <div class="layer" :class="{ on: mode === 'city' }">
      <CesiumView ref="cesiumRef" :active="mode === 'city'" />
    </div>
    <div class="layer" :class="{ on: mode === 'bim' }">
      <BimView ref="bimRef" :active="mode === 'bim'" :model-url="model" @status="status = $event" />
    </div>

    <NavPanel @action="onNav" />

    <div class="toolbar">
      <button @click="toggle">切換：{{ mode === 'city' ? '進入建築內部' : '回到城市' }}</button>
      <span class="status">{{ status }}</span>
    </div>
  </div>
</template>

<style scoped>
.stage {
  position: fixed;
  inset: 0;
}
.layer {
  position: absolute;
  inset: 0;
  opacity: 0;
  pointer-events: none;
  transition: opacity 0.7s ease;
}
.layer.on {
  opacity: 1;
  pointer-events: auto;
}
.toolbar {
  position: absolute;
  top: 12px;
  left: 12px;
  z-index: 10;
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
