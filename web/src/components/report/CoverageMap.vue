<script setup lang="ts">
import { computed } from 'vue'
import type { LocationSummary, ReportSummary } from '../../lib/types'
import StatusPill from '../ui/StatusPill.vue'

const props = defineProps<{ summaries: LocationSummary[]; summary: ReportSummary }>()

const order: Record<string, number> = { Success: 0, Empty: 1, Error: 2, Unavailable: 3 }
const sorted = computed(() =>
  [...props.summaries].sort((a, b) => (order[a.status] ?? 9) - (order[b.status] ?? 9))
)
</script>

<template>
  <div class="report-card card">
    <h3 class="report-card__title">Coverage</h3>
    <ul class="coverage-list">
      <li v-for="s in sorted" :key="s.location" class="coverage-list__row">
        <span class="coverage-list__location">{{ s.location }}</span>
        <StatusPill :status="s.status" />
        <span class="coverage-list__count">
          {{ s.status === 'Success' ? `${s.solicitorCount} firms` : '' }}
        </span>
        <span
          v-if="s.errorMessage"
          class="coverage-list__error"
          :title="s.errorMessage"
          aria-label="Error detail available"
        >⚠</span>
      </li>
    </ul>
  </div>
</template>
