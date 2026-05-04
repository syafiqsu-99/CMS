<template>
  <div class="d-flex flex-column h-100 pa-2">

    <!-- Toolbar -->
    <div class="d-flex align-center ga-2 mb-2 flex-wrap flex-shrink-0">
      <v-select v-model="selectedGroup"
                :items="signalGroupItems"
                label="Signal Group"
                variant="outlined"
                density="compact"
                hide-details
                style="max-width: 200px;" />
      <v-spacer />
      <v-chip color="success" size="small" variant="tonal" prepend-icon="mdi-check-circle-outline">
        {{ onlineCount }} / {{ machines.length }} Online
      </v-chip>
      <v-chip v-if="errorCount > 0" color="error" size="small" variant="tonal" prepend-icon="mdi-alert-circle-outline">
        {{ errorCount }} Error
      </v-chip>
      <span v-if="lastUpdated" class="text-caption text-medium-emphasis">
        Updated: {{ lastUpdated }}
      </span>
      <v-select v-model="pollInterval"
                :items="POLL_INTERVALS"
                item-title="title"
                item-value="value"
                label="Interval"
                variant="outlined"
                density="compact"
                hide-details
                style="max-width: 95px;" />
      <v-btn :color="polling ? 'error' : 'success'"
             :prepend-icon="polling ? 'mdi-pause' : 'mdi-play'"
             variant="flat"
             size="small"
             @click="togglePolling">
        {{ polling ? 'Pause' : 'Start' }}
      </v-btn>
      <v-btn color="primary"
             prepend-icon="mdi-refresh"
             variant="outlined"
             size="small"
             :loading="fetching"
             @click="fetchAllSignals">
        Refresh
      </v-btn>
    </div>

    <!-- Virtual Data Table -->
    <v-data-table-virtual :headers="tableHeaders"
                          :items="tableItems"
                          height="70vh"
                          fixed-header
                          density="compact"
                          hover
                          :row-props="getRowProps"
                          class="border rounded">
      <!-- Dynamic Headers for Signals -->
      <template v-for="sig in visibleSignals"
                :key="'header_' + sig.address"
                v-slot:[`header.sig_${sig.address}`]>
        <div class="d-flex flex-column align-center justify-center py-1">
          <span class="font-weight-bold" style="white-space: nowrap;">{{ sig.name }}</span>
          <v-chip :color="typeColor(sig.dataType)" size="x-small" variant="tonal" class="my-1">
            {{ sig.dataType }}
          </v-chip>
          <span class="text-caption text-medium-emphasis text-monospace">{{ sig.address }}</span>
        </div>
      </template>

      <!-- Status Column -->
      <template v-slot:item.status="{ item }">
        <v-chip :color="item.statusColor" size="x-small" variant="flat">
          {{ item.statusLabel }}
        </v-chip>
      </template>

      <!-- IP Column -->
      <template v-slot:item.ip="{ item }">
        <span class="text-caption text-monospace">{{ item.ip }}</span>
      </template>

      <!-- Dynamic Columns for Signal Data -->
      <template v-for="sig in visibleSignals"
                :key="'item_' + sig.address"
                v-slot:[`item.sig_${sig.address}`]="{ item }">

        <div class="text-center" style="white-space: nowrap;">
          <template v-if="item.hasData">
            <!-- Bit -->
            <template v-if="sig.dataType === 'bit'">
              <v-icon :color="item[`sig_${sig.address}`] ? 'success' : 'error'" size="small">
                {{ item[`sig_${sig.address}`] ? 'mdi-circle' : 'mdi-circle-outline' }}
              </v-icon>
            </template>
            <!-- String -->
            <template v-else-if="sig.dataType === 'string'">
              <span class="d-inline-block text-truncate text-monospace" style="max-width: 120px;">
                {{ item[`sig_${sig.address}`] || '—' }}
              </span>
            </template>
            <!-- Numeric -->
            <template v-else>
              <span class="text-monospace">{{ formatNumeric(item[`sig_${sig.address}`], sig) }}</span>
            </template>
          </template>
          <!-- No Data -->
          <template v-else>
            <span class="text-disabled text-caption">—</span>
          </template>
        </div>
      </template>
    </v-data-table-virtual>

  </div>
</template>

<script setup>
  import { ref, computed, onMounted, onUnmounted, watch } from 'vue';
  import { useMachineStore } from '@/store/machineStore';
  import { SIGNAL_DEFS, POLL_INTERVALS, MACHINE_IP_PREFIX } from '@/utils/constant.js';

  const store = useMachineStore();

  const selectedGroup = ref('All');
  const polling = ref(false);
  const fetching = ref(false);
  const pollInterval = ref(2000);
  const lastUpdated = ref('');
  const machineData = ref({});
  const machineErrors = ref({});
  let pollTimer = null;

  // --- Signal Computations ---
  const signalGroupItems = computed(() => ['All', ...new Set(SIGNAL_DEFS.map(s => s.group))]);

  const visibleSignals = computed(() =>
    selectedGroup.value === 'All'
      ? SIGNAL_DEFS
      : SIGNAL_DEFS.filter(s => s.group === selectedGroup.value)
  );

  // --- Machine Computations ---
  const machines = computed(() => {
    if (store.machineData.length > 0) {
      return store.machineData
        .filter(m => m.machine_name !== 'TEST')
        .map(m => ({ id: m.id_machine, name: m.machine_name }));
    }
    const ids = Object.keys(machineData.value).map(Number).sort((a, b) => a - b);
    return ids.map(id => ({
      id,
      name: machineData.value[id]?.machine_name ?? `M${id}`,
    }));
  });

  const onlineCount = computed(() =>
    machines.value.filter(m => machineData.value[m.id] && !machineErrors.value[m.id]).length
  );

  const errorCount = computed(() =>
    machines.value.filter(m => !!machineErrors.value[m.id]).length
  );

  // --- Table Configuration ---
  const tableHeaders = computed(() => {
    const baseHeaders = [
      { title: 'M#', key: 'id', width: '60px', sortable: false },
      { title: 'Machine', key: 'name', width: '130px', sortable: false },
      { title: 'IP', key: 'ip', width: '120px', sortable: false },
      { title: 'Status', key: 'status', width: '90px', sortable: false }
    ];

    const dynamicHeaders = visibleSignals.value.map(sig => ({
      title: sig.name,
      key: `sig_${sig.address}`,
      minWidth: '130px',
      align: 'center',
      sortable: false
    }));

    return [...baseHeaders, ...dynamicHeaders];
  });

  const tableItems = computed(() => {
    return machines.value.map(m => {
      // Create base row structure
      const row = {
        id: m.id,
        name: m.name,
        ip: `${MACHINE_IP_PREFIX}${219 + m.id}`,
        statusLabel: machineStatusLabel(m.id),
        statusColor: machineStatus(m.id),
        isError: !!machineErrors.value[m.id],
        hasData: !!machineData.value[m.id]
      };

      // Flatten PLC data dynamically into row item so the table can read it natively
      if (machineData.value[m.id]) {
        for (const sig of visibleSignals.value) {
          row[`sig_${sig.address}`] = machineData.value[m.id][sig.address];
        }
      }

      return row;
    });
  });

  // Row styling injected by v-data-table-virtual
  function getRowProps({ item }) {
    if (item.isError) return { class: 'bg-red-lighten-5' };
    return {};
  }

  // --- Helpers ---
  function typeColor(dt) {
    return { bit: 'purple', int: 'blue', float: 'teal', string: 'orange' }[dt] ?? 'grey';
  }

  function machineStatus(id) {
    if (machineErrors.value[id]) return 'error';
    if (machineData.value[id]) return 'success';
    return 'default';
  }

  function machineStatusLabel(id) {
    if (machineErrors.value[id]) return 'Error';
    if (machineData.value[id]) return 'Online';
    return '—';
  }

  function formatNumeric(val, sig) {
    if (val === null || val === undefined) return '—';
    const n = Number(val);
    if (isNaN(n)) return String(val);
    const str = sig.dataType === 'float' ? n.toFixed(2) : n.toLocaleString();
    return sig.unit ? `${str} ${sig.unit}` : str;
  }

  // --- Fetch & Polling ---
  function togglePolling() {
    polling.value ? stopPolling() : startPolling();
  }

  function startPolling() {
    polling.value = true;
    fetchAllSignals();
    pollTimer = setInterval(fetchAllSignals, pollInterval.value);
  }

  function stopPolling() {
    polling.value = false;
    clearInterval(pollTimer);
    pollTimer = null;
  }

  watch(pollInterval, () => {
    if (polling.value) {
      stopPolling();
      startPolling();
    }
  });

  async function fetchAllSignals() {
    fetching.value = true;
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 15000);

    try {
      const res = await fetch('/api/setting/plc-signals', { signal: controller.signal });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const all = await res.json();

      for (const [key, data] of Object.entries(all)) {
        const id = Number(key);
        if (data === null || data === undefined) {
          machineErrors.value[id] = 'Unreachable';
          machineData.value[id] = null;
        } else {
          machineData.value[id] = data;
          machineErrors.value[id] = null;
        }
      }

      lastUpdated.value = new Date().toLocaleTimeString();
    } catch (err) {
      if (err.name === 'AbortError') {
        machines.value.forEach(m => { machineErrors.value[m.id] = 'Timeout'; });
      } else {
        machines.value.forEach(m => { machineErrors.value[m.id] = err.message; });
      }
    } finally {
      clearTimeout(timeout);
      fetching.value = false;
    }
  }

  // --- Lifecycle ---
  onMounted(async () => {
    if (store.machineData.length === 0) {
      await store.loadMachineMaster();
    }
  });

  onUnmounted(() => stopPolling());
</script>
