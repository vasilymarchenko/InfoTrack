<script setup lang="ts">
defineProps<{
  state: 'loading' | 'error' | 'empty'
  message?: string
}>()
</script>

<template>
  <div
    class="state-block"
    :class="`state-block--${state}`"
    role="status"
    :aria-live="state === 'loading' ? 'polite' : 'assertive'"
  >
    <span class="state-block__icon" aria-hidden="true">
      <template v-if="state === 'loading'">⏳</template>
      <template v-else-if="state === 'error'">⚠</template>
      <template v-else>—</template>
    </span>
    <span class="state-block__message">
      {{ message ?? (state === 'loading' ? 'Loading…' : state === 'error' ? 'Something went wrong.' : 'No data.') }}
    </span>
  </div>
</template>
