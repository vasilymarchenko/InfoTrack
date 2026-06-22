<script setup lang="ts">
import { computed } from 'vue'
import type { Solicitor } from '../lib/types'
import { dash } from '../lib/format'
import { firmKey } from '../lib/firmKey'
import ContactIcons from './ui/ContactIcons.vue'

const props = defineProps<{
  solicitors: Solicitor[]
  newFirmKeys?: Set<string>
}>()

const sorted = computed(() =>
  [...props.solicitors].sort((a, b) => {
    if (a.reviewCount === null && b.reviewCount === null) return 0
    if (a.reviewCount === null) return 1
    if (b.reviewCount === null) return -1
    return b.reviewCount - a.reviewCount
  })
)
</script>

<template>
  <section class="lead-list">
    <h2 class="section-title">
      Solicitors
      <span class="count-badge">{{ solicitors.length }}</span>
    </h2>
    <div class="lead-table-wrap">
      <table class="lead-table">
        <thead>
          <tr>
            <th scope="col">Firm</th>
            <th scope="col">Location</th>
            <th scope="col" class="col-num">Reviews</th>
            <th scope="col">Tier</th>
            <th scope="col">Contact</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="s in sorted"
            :key="`${s.firmName}|${s.postcode ?? s.phone ?? s.searchedLocation}`"
            :class="{ 'lead-row--new': newFirmKeys?.has(firmKey(s)) }"
          >
            <td>
              <span v-if="newFirmKeys?.has(firmKey(s))" class="new-badge" aria-label="New firm">NEW</span>
              <a
                v-if="s.profileUrl"
                :href="s.profileUrl"
                target="_blank"
                rel="noopener noreferrer"
              >{{ s.firmName }}</a>
              <span v-else>{{ s.firmName }}</span>
            </td>
            <td>{{ s.searchedLocation }}</td>
            <td class="col-num">{{ dash(s.reviewCount) }}</td>
            <td>
              <span class="tier-badge" :class="`tier-badge--${s.tier.toLowerCase()}`">
                {{ s.tier }}
              </span>
            </td>
            <td>
              <ContactIcons :phone="s.phone" :website-url="s.websiteUrl" />
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>
</template>
