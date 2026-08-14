<template>
  <div class="d-flex flex-column h-100 pa-3" style="min-height: 0;">
    <div class="d-flex align-center flex-shrink-0 mb-3 flex-wrap ga-2">
      <v-icon color="primary" class="mr-2">mdi-database-eye</v-icon>
      <div>
        <div class="text-subtitle-1 font-weight-medium">Database Operation Log</div>
        <div class="text-caption text-medium-emphasis">Recent database operations and any errors, newest first.</div>
      </div>
      <v-spacer />
      <v-text-field v-model="filters.process" label="Process" density="compact" variant="outlined" hide-details
        clearable style="min-width: 160px; max-width: 200px;" @click:clear="onProcessClear" @keyup.enter="fetchLogs" />
      <v-text-field v-model="filters.id_machine" label="Machine ID" density="compact" variant="outlined" hide-details
        clearable type="number" style="min-width: 120px; max-width: 140px;" @click:clear="onMachineClear"
        @keyup.enter="fetchLogs" />
      <v-btn color="primary" variant="tonal" prepend-icon="mdi-refresh" @click="fetchLogs">Refresh</v-btn>
    </div>

    <div class="flex-grow-1 d-flex flex-column overflow-hidden border rounded" style="min-height: 0;">
      <v-data-table-virtual :headers="headers" :items="logs" :loading="loading" height="100%" density="compact"
        fixed-header hover item-value="id" class="fill-table">
        <template #item.time="{ item }">
          {{ formatDate(item.time) }}
        </template>

        <template #item.error_message="{ item }">
          <span v-if="item.error_message" class="text-error text-caption">
            {{ item.error_message }}
          </span>
          <span v-else class="text-disabled">—</span>
        </template>
      </v-data-table-virtual>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'

const headers = [
  { title: 'ID', key: 'id', width: '5%' },
  { title: 'Time', key: 'time', width: '10%' },
  { title: 'Machine', key: 'id_machine', width: '10%' },
  { title: 'Process', key: 'process', width: '20%' },
  { title: 'Details', key: 'details', width: '40%' },
  { title: 'Error', key: 'error_message', width: '15%' },
]

const logs = ref([])
const loading = ref(false)

const filters = reactive({ process: '', id_machine: null })

async function fetchLogs() {
  loading.value = true
  try {
    const params = new URLSearchParams()
    if (filters.process?.trim()) params.append('process', filters.process.trim())
    if (filters.id_machine != null) params.append('id_machine', filters.id_machine)

    const res = await fetch(`/api/setting/db-log?${params}`)
    const data = await res.json()
    logs.value = data
  } finally {
    loading.value = false
  }
}

function onProcessClear() { filters.process = ''; fetchLogs() }
function onMachineClear() { filters.id_machine = null; fetchLogs() }

function formatDate(val) {
  return val ? new Date(val).toLocaleString() : '—'
}

onMounted(fetchLogs)
</script>

<style scoped>
/* Fill the fixed-height wrapper so fixed-header has a bounded scroll area. */
.fill-table {
  flex: 1 1 0;
  min-height: 0;
}

.fill-table :deep(.v-table__wrapper) {
  height: 100%;
  overflow-y: auto;
}
</style>