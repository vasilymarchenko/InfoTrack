<script setup lang="ts">
type AnyStatus = string

defineProps<{ status: AnyStatus }>()

const label: Record<string, string> = {
  Success: 'Success',
  Empty: 'Empty',
  Unavailable: 'Unavailable',
  Error: 'Error',
  Confirmed: 'Confirmed',
  Provisional: 'Provisional',
  Comparable: 'Comparable',
  ScrapeFailed: 'Scrape failed',
  NoBaseline: 'No baseline',
  NotRequested: 'Not requested',
  Active: 'Active',
  ProvisionallyAbsent: 'Possibly left',
  ConfirmedGone: 'Gone',
}

const variant: Record<string, string> = {
  Success: 'success',
  Active: 'success',
  Confirmed: 'success',
  Comparable: 'success',
  Provisional: 'warning',
  ProvisionallyAbsent: 'warning',
  NoBaseline: 'muted',
  NotRequested: 'muted',
  Empty: 'muted',
  Unavailable: 'muted',
  ScrapeFailed: 'danger',
  Error: 'danger',
  ConfirmedGone: 'gone',
}
</script>

<template>
  <span class="pill" :class="`pill--${variant[status] ?? 'muted'}`">
    {{ label[status] ?? status }}
  </span>
</template>
