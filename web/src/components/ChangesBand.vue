<script setup lang="ts">
import { computed, ref } from 'vue'
import type { ChangesResponse, FirmChange } from '../lib/types'
import StatusPill from './ui/StatusPill.vue'

const props = defineProps<{ changes: ChangesResponse }>()

interface FirmEntry { firm: FirmChange; location: string }

const showNew    = ref(false)
const showAbsent = ref(false)

const comparable = computed(() =>
  props.changes.locations.filter(l => l.comparability === 'Comparable')
)

// Non-comparable excluding NotRequested (those are silent)
const nonComparable = computed(() =>
  props.changes.locations.filter(
    l => l.comparability !== 'Comparable' && l.comparability !== 'NotRequested'
  )
)

// Cold start: locations scraped OK but none have a baseline yet
const isColdStart = computed(() =>
  comparable.value.length === 0 &&
  props.changes.locations.some(l => l.comparability === 'NoBaseline')
)

const totalNew = computed(() =>
  comparable.value.reduce((n, l) => n + l.newFirms.length, 0)
)
const totalAbsent = computed(() =>
  comparable.value.reduce((n, l) => n + l.absentFirms.length, 0)
)

const allNew = computed<FirmEntry[]>(() =>
  comparable.value.flatMap(l => l.newFirms.map(f => ({ firm: f, location: l.location })))
)
const allAbsent = computed<FirmEntry[]>(() =>
  comparable.value.flatMap(l => l.absentFirms.map(f => ({ firm: f, location: l.location })))
)

const reasonLabel: Record<string, string> = {
  NoBaseline: 'No baseline yet',
  ScrapeFailed: 'Scrape failed',
}

function firmUrl(firm: FirmChange): string | undefined {
  return firm.firm.profileUrl ?? firm.firm.websiteUrl ?? undefined
}
</script>

<template>
  <section class="changes-band card">
    <h2 class="changes-band__title">Changes since last run</h2>

    <p v-if="isColdStart" class="changes-band__cold-start">
      Baseline set — run again to see new and absent firms flagged here.
    </p>

    <p v-else-if="comparable.length === 0" class="changes-band__none">
      No comparable locations in this run.
    </p>

    <template v-else>
      <div class="changes-band__summary">
        <span class="changes-band__stat changes-band__stat--new">+{{ totalNew }} new</span>
        <span class="changes-band__stat changes-band__stat--absent">−{{ totalAbsent }} absent</span>
        <span v-if="totalNew === 0 && totalAbsent === 0" class="changes-band__no-change">
          No changes detected in comparable locations
        </span>
      </div>

      <div v-if="allNew.length > 0" class="changes-section">
        <button class="changes-section__toggle" @click="showNew = !showNew">
          <span class="changes-section__heading">New firms ({{ allNew.length }})</span>
          <span class="changes-section__chevron" :class="{ 'changes-section__chevron--open': showNew }">›</span>
        </button>
        <ul v-if="showNew" class="changes-list">
          <li
            v-for="{ firm, location } in allNew"
            :key="`new-${firm.firm.firmName}-${location}`"
            class="changes-list__row"
          >
            <a
              v-if="firmUrl(firm)"
              class="changes-list__name changes-list__name--link"
              :href="firmUrl(firm)"
              target="_blank"
              rel="noopener noreferrer"
            >{{ firm.firm.firmName }}</a>
            <span v-else class="changes-list__name">{{ firm.firm.firmName }}</span>
            <span class="changes-list__location">{{ location }}</span>
            <StatusPill :status="firm.confidence" />
          </li>
        </ul>
      </div>

      <div v-if="allAbsent.length > 0" class="changes-section">
        <button class="changes-section__toggle" @click="showAbsent = !showAbsent">
          <span class="changes-section__heading">No longer listed ({{ allAbsent.length }})</span>
          <span class="changes-section__chevron" :class="{ 'changes-section__chevron--open': showAbsent }">›</span>
        </button>
        <ul v-if="showAbsent" class="changes-list">
          <li
            v-for="{ firm, location } in allAbsent"
            :key="`absent-${firm.firm.firmName}-${location}`"
            class="changes-list__row changes-list__row--absent"
          >
            <a
              v-if="firmUrl(firm)"
              class="changes-list__name changes-list__name--link"
              :href="firmUrl(firm)"
              target="_blank"
              rel="noopener noreferrer"
            >{{ firm.firm.firmName }}</a>
            <span v-else class="changes-list__name">{{ firm.firm.firmName }}</span>
            <span class="changes-list__location">{{ location }}</span>
            <StatusPill :status="firm.confidence" />
          </li>
        </ul>
      </div>
    </template>

    <div v-if="nonComparable.length > 0" class="changes-band__footnote">
      <span
        v-for="l in nonComparable"
        :key="l.location"
        class="changes-band__footnote-item"
      >{{ l.location }}: {{ reasonLabel[l.comparability] ?? l.comparability }}</span>
    </div>
  </section>
</template>
