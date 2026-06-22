<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { api, ApiError } from '../lib/api'
import { firmKey } from '../lib/firmKey'
import type { SearchResponse, ChangesResponse, RunListItem } from '../lib/types'
import LocationPicker from '../components/LocationPicker.vue'
import LeadList from '../components/LeadList.vue'
import ReportView from '../components/report/ReportView.vue'
import ChangesBand from '../components/ChangesBand.vue'
import RunHistory from '../components/RunHistory.vue'
import StateBlock from '../components/ui/StateBlock.vue'

type RunState = 'idle' | 'loading' | 'done' | 'error'

const state = ref<RunState>('idle')
const errorMsg = ref('')
const response = ref<SearchResponse | null>(null)
const changes = ref<ChangesResponse | null>(null)
const runs = ref<RunListItem[]>([])
const activeRunId = ref<string | null>(null)

onMounted(async () => {
  try {
    runs.value = await api.listRuns()
  } catch {
    // non-critical — history stays empty
  }
})

async function onRun(locations: string[]) {
  state.value = 'loading'
  errorMsg.value = ''
  response.value = null
  changes.value = null
  activeRunId.value = null
  try {
    response.value = await api.runSearch(locations)
    state.value = 'done'
    activeRunId.value = response.value.runId
    if (response.value.runId) {
      const [ch, rl] = await Promise.allSettled([
        api.getChanges(response.value.runId),
        api.listRuns(),
      ])
      if (ch.status === 'fulfilled') changes.value = ch.value
      if (rl.status === 'fulfilled') runs.value = rl.value
    }
  } catch (e) {
    state.value = 'error'
    if (e instanceof ApiError) {
      errorMsg.value = e.status === 422
        ? 'Please add at least one location.'
        : `Search failed (HTTP ${e.status}).`
    } else {
      errorMsg.value = 'Unexpected error — please try again.'
    }
  }
}

async function onSelectRun(id: string) {
  if (id === activeRunId.value) return
  state.value = 'loading'
  errorMsg.value = ''
  response.value = null
  changes.value = null
  activeRunId.value = id
  const [sr, ch] = await Promise.allSettled([
    api.getRun(id),
    api.getChanges(id),
  ])
  if (sr.status === 'fulfilled') {
    response.value = sr.value
    state.value = 'done'
  } else {
    state.value = 'error'
    errorMsg.value = 'Could not load the selected run.'
    activeRunId.value = null
  }
  if (ch.status === 'fulfilled') changes.value = ch.value
}

const newFirmKeys = computed(() => {
  const keys = new Set<string>()
  if (!changes.value) return keys
  for (const loc of changes.value.locations) {
    if (loc.comparability === 'Comparable') {
      for (const fc of loc.newFirms) {
        keys.add(firmKey(fc.firm))
      }
    }
  }
  return keys
})
</script>

<template>
  <div class="run-view">
    <LocationPicker @run="onRun" />
    <RunHistory
      :runs="runs"
      :active-run-id="activeRunId"
      @select-run="onSelectRun"
    />

    <StateBlock
      v-if="state === 'loading'"
      state="loading"
      message="Scraping solicitors.com — this takes 15–60 s…"
    />
    <StateBlock
      v-else-if="state === 'error'"
      state="error"
      :message="errorMsg"
    />

    <template v-if="state === 'done' && response">
      <p v-if="response.runId === null" class="notice notice--warn">
        Results not saved — database was unavailable. Data shown below is still valid.
      </p>
      <ChangesBand v-if="changes" :changes="changes" />
      <ReportView :report="response.report" :solicitors="response.result.uniqueSolicitors" />
      <LeadList :solicitors="response.report.displaySolicitors" :new-firm-keys="newFirmKeys" />
    </template>
  </div>
</template>
