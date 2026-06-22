<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { api, ApiError } from '../lib/api'
import type { FirmProjection } from '../lib/types'
import FirmFilters from '../components/firms/FirmFilters.vue'
import FirmRow from '../components/firms/FirmRow.vue'
import StateBlock from '../components/ui/StateBlock.vue'

type LoadState = 'loading' | 'done' | 'error'
const loadState = ref<LoadState>('loading')
const firms = ref<FirmProjection[]>([])
const errorMsg = ref('')

async function load(status?: string, addedSince?: string) {
  loadState.value = 'loading'
  errorMsg.value = ''
  try {
    firms.value = await api.getFirms(status || undefined, addedSince || undefined)
    loadState.value = 'done'
  } catch (e) {
    loadState.value = 'error'
    errorMsg.value = e instanceof ApiError && e.status === 422
      ? 'Invalid status filter.'
      : 'Could not load firms.'
  }
}

onMounted(() => load())

function onFiltersChange(filters: { status: string; addedSince: string }) {
  // Convert date-only value to ISO 8601 with time + UTC offset
  const addedSinceIso = filters.addedSince ? filters.addedSince + 'T00:00:00Z' : ''
  load(filters.status, addedSinceIso)
}
</script>

<template>
  <div class="firms-view">
    <h2 class="section-title">Firms</h2>
    <FirmFilters @change="onFiltersChange" />

    <StateBlock v-if="loadState === 'loading'" state="loading" message="Loading firms…" />
    <StateBlock v-else-if="loadState === 'error'" state="error" :message="errorMsg" />

    <template v-else>
      <StateBlock v-if="firms.length === 0" state="empty" message="No firms match the current filters." />
      <div v-else class="firm-list">
        <FirmRow v-for="firm in firms" :key="firm.firmId" :firm="firm" />
      </div>
    </template>
  </div>
</template>

