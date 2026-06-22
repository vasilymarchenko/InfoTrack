<script setup lang="ts">
import type { RunListItem } from '../lib/types'
import { formatDate } from '../lib/format'

defineProps<{
  runs: RunListItem[]
  activeRunId?: string | null
}>()

const emit = defineEmits<{
  selectRun: [id: string]
}>()
</script>

<template>
  <div v-if="runs.length > 0" class="run-history">
    <h3 class="run-history__label">Run history</h3>
    <div class="run-history__strip">
      <button
        v-for="run in runs"
        :key="run.runId"
        class="run-history__item"
        :class="{ 'run-history__item--active': run.runId === activeRunId }"
        :aria-label="`View run from ${formatDate(run.runAtUtc)}, ${run.locationCount} cities, ${run.totalUniqueFirms} firms`"
        :aria-pressed="run.runId === activeRunId"
        @click="emit('selectRun', run.runId)"
      >
        <span class="run-history__date">{{ formatDate(run.runAtUtc) }}</span>
        <span class="run-history__meta">{{ run.locationCount }} cities · {{ run.totalUniqueFirms }} firms</span>
      </button>
    </div>
  </div>
</template>
