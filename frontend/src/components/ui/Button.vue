<script setup lang="ts">
import { cva } from 'class-variance-authority'
import { cn } from '@/lib/utils'

const variants = cva(
  'ui inline-flex items-center justify-center gap-1.5 whitespace-nowrap rounded-lg border border-solid text-sm font-medium backdrop-blur transition-colors cursor-pointer select-none focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-400 disabled:opacity-50 disabled:pointer-events-none',
  {
    variants: {
      variant: {
        default: 'bg-blue-600 border-blue-500 text-white hover:bg-blue-500',
        secondary: 'bg-[var(--panel)] border-[var(--line)] text-white hover:bg-white/15',
        destructive: 'bg-red-600/90 border-red-400/60 text-white hover:bg-red-500',
        ghost: 'bg-transparent border-transparent text-white hover:bg-white/15',
      },
      size: {
        default: 'h-9 px-4',
        sm: 'h-8 px-3 text-[13px]',
        icon: 'h-9 w-9 text-base',
      },
    },
    defaultVariants: { variant: 'secondary', size: 'default' },
  },
)

const props = defineProps<{
  variant?: 'default' | 'secondary' | 'destructive' | 'ghost'
  size?: 'default' | 'sm' | 'icon'
  class?: string
}>()
</script>

<template>
  <button :class="cn(variants({ variant: props.variant, size: props.size }), props.class)">
    <slot />
  </button>
</template>
