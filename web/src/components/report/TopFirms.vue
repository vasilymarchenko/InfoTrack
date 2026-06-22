<script setup lang="ts">
import type { TopFirm } from '../../lib/types'
import { formatCount } from '../../lib/format'

const props = defineProps<{
  firms: TopFirm[]
  firmLinks?: Record<string, string>
}>()

function normalise(value: string): string {
  return value
    .toLowerCase()
    .replace(/[^a-z0-9 ]/g, '')
    .replace(/\s+/g, ' ')
    .trim()
}

function firmLocationKey(firm: TopFirm): string {
  return `${normalise(firm.firmName)}|${normalise(firm.location)}`
}

function firmUrl(firm: TopFirm): string | undefined {
  return props.firmLinks?.[firmLocationKey(firm)]
}
</script>

<template>
  <div class="report-card card">
    <h3 class="report-card__title">
      Call first
      <span class="report-card__sub">by review prominence — not a star rating</span>
    </h3>
    <p v-if="firms.length === 0" class="placeholder">No firms with reviews</p>
    <ol v-else class="top-firms-list">
      <li v-for="f in firms" :key="f.firmName + f.location" class="top-firms-list__row">
        <a
          v-if="firmUrl(f)"
          class="top-firms-list__name"
          :href="firmUrl(f)"
          target="_blank"
          rel="noopener noreferrer"
        >{{ f.firmName }}</a>
        <span v-else class="top-firms-list__name">{{ f.firmName }}</span>
        <span class="top-firms-list__location">{{ f.location }}</span>
        <span class="top-firms-list__reviews">{{ formatCount(f.reviewCount) }}</span>
      </li>
    </ol>
  </div>
</template>
