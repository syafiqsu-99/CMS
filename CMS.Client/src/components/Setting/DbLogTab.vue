<template>
  <v-card>
    <v-card-title class="d-flex align-center justify-space-between flex-wrap ga-2">
      <span>Database Operation Log</span>
      <div class="d-flex align-center ga-2">
        <v-text-field v-model="filters.process"
                      label="Process"
                      density="compact"
                      hide-details
                      clearable
                      style="min-width: 160px"
                      @click:clear="onProcessClear"
                      @keyup.enter="fetchLogs" />
        <v-text-field v-model="filters.id_machine"
                      label="Machine ID"
                      density="compact"
                      hide-details
                      clearable
                      type="number"
                      style="min-width: 120px"
                      @click:clear="onMachineClear"
                      @keyup.enter="fetchLogs" />
        <v-btn icon="mdi-refresh" variant="text" @click="fetchLogs" />
      </div>
    </v-card-title>

    <v-data-table-virtual :headers="headers"
                          :items="logs"
                          :loading="loading"
                          height="70vh"
                          density="compact"
                          fixed-header
                          hover
                          item-value="id">
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
  </v-card>
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
