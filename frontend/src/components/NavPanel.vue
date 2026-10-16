<script setup lang="ts">
import { ChevronDown, ChevronUp, House, Minus, Plus, RotateCcw, RotateCw } from 'lucide-vue-next'
import type { Component } from 'vue'
import Button from './ui/Button.vue'

export type NavAction =
  | 'zoomIn' | 'zoomOut'
  | 'rotateLeft' | 'rotateRight'
  | 'tiltUp' | 'tiltDown'
  | 'reset'

const emit = defineEmits<{ (e: 'action', a: NavAction): void }>()

const buttons: { a: NavAction; icon: Component; title: string }[] = [
  { a: 'zoomIn', icon: Plus, title: '放大' },
  { a: 'zoomOut', icon: Minus, title: '縮小' },
  { a: 'rotateLeft', icon: RotateCcw, title: '向左旋轉' },
  { a: 'rotateRight', icon: RotateCw, title: '向右旋轉' },
  { a: 'tiltUp', icon: ChevronUp, title: '視角抬高（更俯視）' },
  { a: 'tiltDown', icon: ChevronDown, title: '視角放低（更平視）' },
  { a: 'reset', icon: House, title: '回到初始視角' },
]
</script>

<template>
  <div class="ui absolute right-3 top-[60px] z-10 flex flex-col gap-1 rounded-xl border border-solid border-[var(--line)] bg-[var(--panel)] p-1 backdrop-blur">
    <Button v-for="b in buttons" :key="b.a" variant="ghost" size="icon" :title="b.title" @click="emit('action', b.a)">
      <component :is="b.icon" class="size-5" />
    </Button>
  </div>
</template>
