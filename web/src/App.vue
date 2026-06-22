<script setup lang="ts">
import { ref } from 'vue'
import RunView from './views/RunView.vue'
import FirmsView from './views/FirmsView.vue'

type View = 'run' | 'firms'
const activeView = ref<View>('run')
</script>

<template>
  <header class="app-header">
    <span class="app-logo">InfoTrack</span>
    <nav class="app-nav">
      <button
        class="nav-btn"
        :class="{ active: activeView === 'run' }"
        aria-label="Search view"
        @click="activeView = 'run'"
      >
        Search
      </button>
      <button
        class="nav-btn"
        :class="{ active: activeView === 'firms' }"
        aria-label="Firms watchlist view"
        @click="activeView = 'firms'"
      >
        Firms
      </button>
    </nav>
  </header>

  <main class="app-main">
    <RunView v-if="activeView === 'run'" />
    <FirmsView v-else />
  </main>
</template>
