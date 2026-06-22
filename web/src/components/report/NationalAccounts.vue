<script setup lang="ts">
import type { MultiLocationFirm } from '../../lib/types'

defineProps<{ firms: MultiLocationFirm[] }>()
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
        <span class="national-list__name">{{ f.normalisedFirmName }}</span>
        <span class="national-list__locations">{{ f.locations.join(' · ') }}</span>
      </li>
    </ul>
  </div>
</template>
