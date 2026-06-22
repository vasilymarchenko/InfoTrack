<script setup lang="ts">
import { computed } from 'vue'
import type { Contactability } from '../../lib/types'
import { formatPercent, formatCount } from '../../lib/format'

const props = defineProps<{ contactability: Contactability }>()

const unreachable = computed(
  () => props.contactability.totalFirms - props.contactability.withPhoneOrWebsite
)
</script>

<template>
  <div class="report-card card">
    <h3 class="report-card__title">Workability</h3>
    <ul class="workability-list">
      <li class="workability-list__row">
        <span class="workability-list__label">Phone</span>
        <span class="workability-list__value">
          {{ formatCount(contactability.withPhone) }}
          <span class="workability-list__pct">({{ formatPercent(contactability.percentWithPhone) }})</span>
        </span>
      </li>
      <li class="workability-list__row">
        <span class="workability-list__label">Website</span>
        <span class="workability-list__value">
          {{ formatCount(contactability.withWebsite) }}
          <span class="workability-list__pct">({{ formatPercent(contactability.percentWithWebsite) }})</span>
        </span>
      </li>
      <li class="workability-list__row">
        <span class="workability-list__label">Phone or website</span>
        <span class="workability-list__value">
          {{ formatCount(contactability.withPhoneOrWebsite) }}
          <span class="workability-list__pct">({{ formatPercent(contactability.percentWithPhoneOrWebsite) }})</span>
        </span>
      </li>
      <li class="workability-list__row workability-list__row--muted">
        <span class="workability-list__label">No way to reach</span>
        <span class="workability-list__value">{{ formatCount(unreachable) }}</span>
      </li>
    </ul>
  </div>
</template>
