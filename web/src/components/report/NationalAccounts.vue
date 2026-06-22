<script setup lang="ts">
import type { MultiLocationFirm } from '../../lib/types'

const props = defineProps<{
  firms: MultiLocationFirm[]
  firmLinks?: Record<string, string>
}>()

function normalise(value: string): string {
  return value
    .toLowerCase()
    .replace(/[^a-z0-9 ]/g, '')
    .replace(/\s+/g, ' ')
    .trim()
}

function firmUrl(firm: MultiLocationFirm): string | undefined {
  return props.firmLinks?.[normalise(firm.normalisedFirmName)]
}
</script>

<template>
  <div class="report-card card">
    <h3 class="report-card__title">
      National accounts
      <span class="report-card__sub">present in 2+ cities</span>
    </h3>
    <p v-if="firms.length === 0" class="placeholder">No multi-location firms found</p>
    <ul v-else class="national-list">
      <li v-for="f in firms" :key="f.normalisedFirmName" class="national-list__row">
        <a
          v-if="firmUrl(f)"
          class="national-list__name"
          :href="firmUrl(f)"
          target="_blank"
          rel="noopener noreferrer"
        >{{ f.normalisedFirmName }}</a>
        <span v-else class="national-list__name">{{ f.normalisedFirmName }}</span>
        <span class="national-list__locations">{{ f.locations.join(' · ') }}</span>
      </li>
    </ul>
  </div>
</template>
