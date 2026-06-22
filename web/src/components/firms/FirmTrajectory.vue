<script setup lang="ts">
import { computed } from 'vue'
import type { FirmHistory } from '../../lib/types'
import { formatCount } from '../../lib/format'

const props = defineProps<{ history: FirmHistory }>()

const validPoints = computed(() =>
  props.history.points
    .filter(p => p.reviewCount !== null)
    .map(p => ({ ...p, reviewCount: p.reviewCount as number }))
)

// Build SVG polyline points string normalised to a 120×30 viewport
const svgPolyline = computed(() => {
  if (validPoints.value.length < 2) return ''
  const counts = validPoints.value.map(p => p.reviewCount)
  const min = Math.min(...counts)
  const max = Math.max(...counts)
  const range = max - min || 1
  const W = 120, H = 30, PAD = 4
  return validPoints.value
    .map((p, i) => {
      const x = PAD + (i / (validPoints.value.length - 1)) * (W - PAD * 2)
      const y = PAD + (1 - (p.reviewCount - min) / range) * (H - PAD * 2)
      return `${x.toFixed(1)},${y.toFixed(1)}`
    })
    .join(' ')
})

const trendConfig = computed(() => {
  const t = props.history.overallReviewTrend
  return {
    Rising:  { arrow: '↑', cls: 'trend--rising',  label: 'Rising' },
    Falling: { arrow: '↓', cls: 'trend--falling', label: 'Falling' },
    Steady:  { arrow: '→', cls: 'trend--steady',  label: 'Steady' },
    Unknown: { arrow: '—', cls: 'trend--unknown', label: 'Unknown' },
  }[t] ?? { arrow: '—', cls: 'trend--unknown', label: t }
})
</script>

<template>
  <div class="trajectory">
    <div v-if="validPoints.length >= 2" class="trajectory__sparkline-wrap">
      <svg
        class="trajectory__sparkline"
        viewBox="0 0 120 30"
        aria-hidden="true"
        preserveAspectRatio="none"
      >
        <polyline
          :points="svgPolyline"
          fill="none"
          stroke="currentColor"
          stroke-width="1.5"
          stroke-linejoin="round"
          stroke-linecap="round"
        />
      </svg>
    </div>

    <div class="trajectory__meta">
      <span
        class="trend-label"
        :class="trendConfig.cls"
        :aria-label="`Review trend: ${trendConfig.label}`"
      >
        {{ trendConfig.arrow }} {{ trendConfig.label }}
      </span>
      <span v-if="validPoints.length >= 2" class="trajectory__range">
        {{ formatCount(validPoints[0].reviewCount) }} → {{ formatCount(validPoints[validPoints.length - 1].reviewCount) }} reviews
      </span>
      <span v-else class="trajectory__no-data">Not enough review data</span>
    </div>
  </div>
</template>
