<script setup lang="ts">
defineProps<{
  phone: string | null
  websiteUrl: string | null
}>()
</script>

<template>
  <span class="contact-icons">
    <a
      v-if="phone"
      :href="`tel:${phone}`"
      class="contact-icon contact-icon--phone"
      :aria-label="`Call ${phone}`"
      :title="phone"
    >☎</a>
    <span
      v-else
      class="contact-icon contact-icon--missing"
      aria-label="No phone"
      title="No phone"
    >–</span>

    <a
      v-if="websiteUrl"
      :href="websiteUrl"
      target="_blank"
      rel="noopener noreferrer"
      class="contact-icon contact-icon--web"
      aria-label="Visit website"
      title="Website"
    >🌐</a>
    <span
      v-else
      class="contact-icon contact-icon--missing"
      aria-label="No website"
      title="No website"
    >–</span>
  </span>
</template>
