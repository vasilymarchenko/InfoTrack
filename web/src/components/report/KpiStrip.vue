<script setup lang="ts">
import type { ReportSummary, Contactability } from '../../lib/types'
import { formatPercent, formatCount } from '../../lib/format'

defineProps<{ summary: ReportSummary; contactability: Contactability }>()
</script>

<template>
  <div class="kpi-strip">
    <div class="kpi">
      <span class="kpi__value">{{ formatCount(summary.totalUniqueSolicitors) }}</span>
      <span class="kpi__label">Unique firms</span>
    </div>
    <div class="kpi">
      <span class="kpi__value">{{ summary.successfulLocations }}/{{ summary.totalLocationsRequested }}</span>
      <span class="kpi__label">Cities covered</span>
    </div>
    <div class="kpi">
      <span class="kpi__value">{{ formatPercent(contactability.percentWithPhoneOrWebsite) }}</span>
      <span class="kpi__label">Reachable</span>
    </div>
    <div class="kpi">
      <span class="kpi__value">{{ formatPercent(contactability.percentWithPhone) }}</span>
      <span class="kpi__label">Have phone</span>
    </div>
    <div class="kpi">
      <span class="kpi__value">{{ formatPercent(contactability.percentWithWebsite) }}</span>
      <span class="kpi__label">Have website</span>
    </div>
  </div>
</template>
