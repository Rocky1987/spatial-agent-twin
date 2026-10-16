<script setup lang="ts">
import { Check, ChevronDown } from 'lucide-vue-next'
import {
  SelectContent, SelectItem, SelectItemIndicator, SelectItemText, SelectPortal,
  SelectRoot, SelectTrigger, SelectValue, SelectViewport,
} from 'reka-ui'

defineOptions({ inheritAttrs: false })
defineProps<{ options: { value: string; label: string }[]; placeholder?: string }>()
const model = defineModel<string>({ required: true })
</script>

<template>
  <SelectRoot v-model="model">
    <SelectTrigger
      v-bind="$attrs"
      class="ui inline-flex h-9 min-w-0 max-w-full items-center justify-between gap-2 rounded-lg border border-solid border-[var(--line)] bg-[var(--panel)] px-3 text-sm text-white backdrop-blur transition-colors hover:bg-white/15 focus-visible:outline-2 focus-visible:outline-blue-400 cursor-pointer"
    >
      <SelectValue :placeholder="placeholder" class="truncate" />
      <ChevronDown class="size-4 shrink-0 opacity-70" />
    </SelectTrigger>
    <SelectPortal>
      <SelectContent
        position="popper"
        :side-offset="4"
        class="ui z-[1000] max-h-72 min-w-[var(--reka-select-trigger-width)] overflow-hidden rounded-lg border border-solid border-[var(--line)] bg-[var(--panel-solid)] text-white shadow-xl"
      >
        <SelectViewport class="p-1">
          <SelectItem
            v-for="o in options"
            :key="o.value"
            :value="o.value"
            class="relative flex cursor-pointer select-none items-center rounded-md py-1.5 pl-7 pr-3 text-sm outline-none data-[highlighted]:bg-blue-600"
          >
            <SelectItemIndicator class="absolute left-2 inline-flex items-center"><Check class="size-4" /></SelectItemIndicator>
            <SelectItemText>{{ o.label }}</SelectItemText>
          </SelectItem>
        </SelectViewport>
      </SelectContent>
    </SelectPortal>
  </SelectRoot>
</template>
