<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { api } from '../lib/api'

const emit = defineEmits<{
  run: [locations: string[]]
}>()

const locations = ref<string[]>([])
const inputValue = ref('')
const loadError = ref(false)

onMounted(async () => {
  try {
    locations.value = await api.getLocations()
  } catch {
    loadError.value = true
  }
})

function add() {
  const val = inputValue.value.trim()
  if (!val) return
  if (!locations.value.some(l => l.toLowerCase() === val.toLowerCase())) {
    locations.value = [...locations.value, val]
  }
  inputValue.value = ''
}

function remove(loc: string) {
  locations.value = locations.value.filter(l => l !== loc)
}

function onInputKeydown(e: KeyboardEvent) {
  if (e.key === 'Enter') {
    e.preventDefault()
    add()
  }
}

function run() {
  if (locations.value.length === 0) return
  emit('run', [...locations.value])
}
</script>

<template>
  <div class="location-picker card">
    <div class="location-picker__header">
      <h2 class="location-picker__title">Locations</h2>
      <button
        class="btn-primary"
        :disabled="locations.length === 0"
        aria-label="Run search for selected locations"
        @click="run"
      >
        Run Search
      </button>
    </div>

    <p v-if="loadError" class="notice notice--error">
      Could not load default locations — add cities manually.
    </p>

    <div class="chip-list" role="list" aria-label="Selected locations">
      <span
        v-for="loc in locations"
        :key="loc"
        class="chip"
        role="listitem"
      >
        {{ loc }}
        <button
          class="chip__remove"
          :aria-label="`Remove ${loc}`"
          @click="remove(loc)"
        >×</button>
      </span>
      <span v-if="locations.length === 0" class="chip-list__empty">
        No locations selected — add one below.
      </span>
    </div>

    <div class="location-picker__input-row">
      <input
        v-model="inputValue"
        class="text-input"
        type="text"
        placeholder="Add a city…"
        aria-label="Add a city to the location list"
        @keydown="onInputKeydown"
      />
      <button
        class="btn-secondary"
        :disabled="!inputValue.trim()"
        aria-label="Add city to list"
        @click="add"
      >
        Add
      </button>
    </div>
  </div>
</template>
