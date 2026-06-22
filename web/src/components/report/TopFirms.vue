<script setup lang="ts">
import type { TopFirm } from '../../lib/types'
import { formatCount } from '../../lib/format'

defineProps<{ firms: TopFirm[] }>()
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
        <span class="top-firms-list__name">{{ f.firmName }}</span>
        <span class="top-firms-list__location">{{ f.location }}</span>
        <span class="top-firms-list__reviews">{{ formatCount(f.reviewCount) }}</span>
      </li>
    </ol>
  </div>
</template>
