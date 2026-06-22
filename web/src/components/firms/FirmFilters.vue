<script setup lang="ts">
import { ref, watch } from 'vue'

const emit = defineEmits<{
  change: [filters: { status: string; addedSince: string }]
}>()

const STATUS_TABS = [
  { label: 'All', value: '' },
  { label: 'Active', value: 'active' },
  { label: 'Possibly left', value: 'provisional' },
  { label: 'Gone', value: 'gone' },
]

const status = ref('')
const addedSince = ref('')

watch([status, addedSince], () => {
  emit('change', { status: status.value, addedSince: addedSince.value })
})
</script>

<template>
  <div class="firm-filters">
    <div class="firm-filters__tabs" role="tablist" aria-label="Filter by status">
      <button
        v-for="tab in STATUS_TABS"
        :key="tab.value"
        class="firm-filters__tab"
        :class="{ 'firm-filters__tab--active': status === tab.value }"
        role="tab"
        :aria-selected="status === tab.value"
        @click="status = tab.value"
      >
        {{ tab.label }}
      </button>
    </div>
    <div class="firm-filters__date-row">
      <label class="firm-filters__date-label" for="added-since">First seen on or after</label>
      <input
        id="added-since"
        v-model="addedSince"
        class="text-input firm-filters__date-input"
        type="date"
        aria-label="Filter firms first seen on or after this date"
      />
      <button
        v-if="addedSince"
        class="btn-secondary"
        aria-label="Clear date filter"
        @click="addedSince = ''"
      >
        Clear
      </button>
    </div>
  </div>
</template>
