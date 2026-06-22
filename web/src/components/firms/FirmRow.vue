<script setup lang="ts">
import { ref } from 'vue'
import type { FirmProjection, FirmHistory } from '../../lib/types'
import { api, ApiError } from '../../lib/api'
import { formatDate } from '../../lib/format'
import StatusPill from '../ui/StatusPill.vue'
import StateBlock from '../ui/StateBlock.vue'
import FirmTrajectory from './FirmTrajectory.vue'

const props = defineProps<{ firm: FirmProjection }>()

type DetailState = 'collapsed' | 'loading' | 'done' | 'error' | 'not-found'
const detailState = ref<DetailState>('collapsed')
const history = ref<FirmHistory | null>(null)

async function toggle() {
  if (detailState.value !== 'collapsed') {
    detailState.value = 'collapsed'
    return
  }
  detailState.value = 'loading'
  try {
    const detail = await api.getFirm(props.firm.firmId)
    history.value = detail.history
    detailState.value = 'done'
  } catch (e) {
    detailState.value = e instanceof ApiError && e.status === 404 ? 'not-found' : 'error'
  }
}
</script>

<template>
  <div class="firm-row card">
    <div class="firm-row__header">
      <div class="firm-row__identity">
        <a
          v-if="firm.latest.profileUrl"
          :href="firm.latest.profileUrl"
          target="_blank"
          rel="noopener noreferrer"
          class="firm-row__name"
        >{{ firm.latest.firmName }}</a>
        <span v-else class="firm-row__name">{{ firm.latest.firmName }}</span>
        <StatusPill :status="firm.rollupStatus" />
      </div>

      <div class="firm-row__meta">
        <span class="firm-row__meta-item">First seen {{ formatDate(firm.firstSeenAt) }}</span>
        <span class="firm-row__meta-item">Last seen {{ formatDate(firm.lastSeenAt) }}</span>
      </div>

      <div class="firm-row__locations">
        <span
          v-for="loc in firm.locations"
          :key="loc.location"
          class="firm-row__location-tag"
        >
          {{ loc.location }}
          <StatusPill :status="loc.status" />
        </span>
      </div>

      <button
        class="firm-row__toggle btn-secondary"
        :aria-expanded="detailState !== 'collapsed'"
        :aria-label="`${detailState === 'collapsed' ? 'Expand' : 'Collapse'} details for ${firm.latest.firmName}`"
        @click="toggle"
      >
        {{ detailState === 'collapsed' ? 'Details ▾' : 'Close ▴' }}
      </button>
    </div>

    <div v-if="detailState !== 'collapsed'" class="firm-row__detail">
      <StateBlock v-if="detailState === 'loading'" state="loading" message="Loading history…" />
      <StateBlock v-else-if="detailState === 'error'" state="error" message="Could not load firm details." />
      <StateBlock v-else-if="detailState === 'not-found'" state="empty" message="No history found for this firm." />
      <FirmTrajectory v-else-if="detailState === 'done' && history" :history="history" />
    </div>
  </div>
</template>
