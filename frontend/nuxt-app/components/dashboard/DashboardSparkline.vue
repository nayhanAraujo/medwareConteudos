<template>
  <svg viewBox="0 0 120 32" class="w-full h-8" preserveAspectRatio="none" aria-hidden="true">
    <defs>
      <linearGradient :id="gradientId" x1="0" y1="0" x2="0" y2="1">
        <stop offset="0%" :stop-color="color" stop-opacity="0.28" />
        <stop offset="100%" :stop-color="color" stop-opacity="0" />
      </linearGradient>
    </defs>
    <polygon :points="areaPoints" :fill="`url(#${gradientId})`" />
    <polyline
      :points="linePoints"
      fill="none"
      :stroke="color"
      stroke-width="1.8"
      stroke-linecap="round"
      stroke-linejoin="round"
    />
  </svg>
</template>

<script setup lang="ts">
const props = defineProps<{
  values: number[]
  color: string
}>()

const gradientId = computed(() => `spark-${props.color.replace('#', '')}`)

const coords = computed(() => {
  const values = props.values.length ? props.values : [0, 0]
  const min = Math.min(...values)
  const max = Math.max(...values)
  const span = Math.max(max - min, 1)
  return values.map((value, index) => {
    const x = values.length === 1 ? 60 : (index / (values.length - 1)) * 120
    const y = 28 - ((value - min) / span) * 24
    return { x, y }
  })
})

const linePoints = computed(() => coords.value.map(point => `${point.x},${point.y}`).join(' '))
const areaPoints = computed(() => {
  const line = coords.value
  if (!line.length) return ''
  return `0,32 ${line.map(point => `${point.x},${point.y}`).join(' ')} 120,32`
})
</script>
