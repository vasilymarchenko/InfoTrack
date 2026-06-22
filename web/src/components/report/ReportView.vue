<script setup lang="ts">
import { computed } from 'vue'
import type { SearchReport, Solicitor } from '../../lib/types'
import KpiStrip from './KpiStrip.vue'
import TopFirms from './TopFirms.vue'
import NationalAccounts from './NationalAccounts.vue'
import Workability from './Workability.vue'
import CoverageMap from './CoverageMap.vue'

const props = defineProps<{
  report: SearchReport
  solicitors: Solicitor[]
}>()

function normalise(value: string): string {
  return value
    .toLowerCase()
    .replace(/[^a-z0-9 ]/g, '')
    .replace(/\s+/g, ' ')
    .trim()
}

const firmLocationLinks = computed<Record<string, string>>(() => {
  const links: Record<string, string> = {}
  for (const s of props.solicitors) {
    const url = s.profileUrl ?? s.websiteUrl
    if (!url) continue
    const key = `${normalise(s.firmName)}|${normalise(s.searchedLocation)}`
    if (!links[key]) links[key] = url
  }
  return links
})

const firmLinks = computed<Record<string, string>>(() => {
  const links: Record<string, string> = {}
  for (const s of props.solicitors) {
    const url = s.profileUrl ?? s.websiteUrl
    if (!url) continue
    const key = normalise(s.firmName)
    if (!links[key]) links[key] = url
  }
  return links
})
</script>

<template>
  <section class="report-view">
    <KpiStrip :summary="report.summary" :contactability="report.contactability" />
    <div class="report-grid">
      <TopFirms :firms="report.topFirmsByReviewCount" :firm-links="firmLocationLinks" />
      <NationalAccounts :firms="report.multiLocationFirms" :firm-links="firmLinks" />
      <Workability :contactability="report.contactability" />
      <CoverageMap :summaries="report.locationSummaries" :summary="report.summary" />
    </div>
  </section>
</template>
