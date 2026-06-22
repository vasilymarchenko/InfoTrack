<script setup lang="ts">
import type { SearchReport } from '../../lib/types'
import KpiStrip from './KpiStrip.vue'
import TopFirms from './TopFirms.vue'
import NationalAccounts from './NationalAccounts.vue'
import Workability from './Workability.vue'
import CoverageMap from './CoverageMap.vue'

defineProps<{ report: SearchReport }>()
</script>

<template>
  <section class="report-view">
    <KpiStrip :summary="report.summary" :contactability="report.contactability" />
    <div class="report-grid">
      <TopFirms :firms="report.topFirmsByReviewCount" />
      <NationalAccounts :firms="report.multiLocationFirms" />
      <Workability :contactability="report.contactability" />
      <CoverageMap :summaries="report.locationSummaries" :summary="report.summary" />
    </div>
  </section>
</template>
