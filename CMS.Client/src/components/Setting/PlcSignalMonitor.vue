<template>
  <v-container fluid class="pa-2 d-flex flex-column" style="height: 100%;">
    <!-- Toolbar -->
    <div class="d-flex align-center ga-2 mb-2 flex-wrap">
      <v-select v-model="selectedGroup"
                :items="signalGroupItems"
                label="Signal Group"
                variant="outlined"
                density="compact"
                hide-details
                style="max-width: 200px;" />
      <v-spacer />
      <v-chip color="success" size="small" variant="tonal" prepend-icon="mdi-check-circle-outline">
        {{ onlineCount }} / {{ MACHINE_COUNT }} Online
      </v-chip>
      <v-chip v-if="errorCount > 0"
              color="error"
              size="small"
              variant="tonal"
              prepend-icon="mdi-alert-circle-outline">
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

    <!-- Signal table -->
    <div class="flex-grow-1" style="overflow: auto; min-height: 0;">
      <table class="signal-table">
        <thead>
          <tr>
            <th class="col-frozen col-machine">Machine</th>
            <th class="col-frozen col-ip">IP</th>
            <th class="col-frozen col-status">Status</th>
            <th v-for="sig in visibleSignals" :key="sig.address" class="col-signal">
              <div class="sig-header">
                <span class="sig-name">{{ sig.name }}</span>
                <v-chip :color="typeColor(sig.dataType)" size="x-small" variant="tonal">
                  {{ sig.dataType }}
                </v-chip>
                <span class="sig-addr">{{ sig.address }}</span>
              </div>
            </th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="m in MACHINE_COUNT" :key="m" :class="['machine-row', machineRowClass(m)]">
            <td class="col-frozen col-machine font-weight-bold text-body-2">M{{ m }}</td>
            <td class="col-frozen col-ip text-caption" style="font-family: monospace;">
              {{ MACHINE_IP_PREFIX }}{{ 219 + m }}
            </td>
            <td class="col-frozen col-status">
              <v-chip :color="machineStatus(m)" size="x-small" variant="flat">
                {{ machineStatusLabel(m) }}
              </v-chip>
            </td>
            <td v-for="sig in visibleSignals"
                :key="sig.address"
                :class="cellClass(m, sig)">
              <template v-if="machineData[m]">
                <template v-if="sig.dataType === 'bit'">
                  <v-icon :color="machineData[m][sig.address] ? 'success' : 'error'"
                          size="16">
                    {{ machineData[m][sig.address] ? 'mdi-circle' : 'mdi-circle-outline' }}
                  </v-icon>
                </template>
                <template v-else-if="sig.dataType === 'string'">
                  <span class="cell-string">{{ machineData[m][sig.address] || '—' }}</span>
                </template>
                <template v-else>
                  <span class="cell-numeric">{{ formatNumeric(machineData[m][sig.address], sig) }}</span>
                </template>
              </template>
              <template v-else>
                <span class="text-disabled text-caption">—</span>
              </template>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </v-container>
</template>

<script setup>
import { ref, computed, onUnmounted, watch } from 'vue';
import { SIGNAL_DEFS, POLL_INTERVALS, MACHINE_COUNT, MACHINE_IP_PREFIX } from '@/utils/constant.js';

const selectedGroup = ref('All');
const polling       = ref(false);
const fetching      = ref(false);
const pollInterval  = ref(2000);
const lastUpdated   = ref('');
const machineData   = ref({});   // { 1: {addr: val, ...}, 2: null, ... }
const machineErrors = ref({});

let pollTimer = null;

const signalGroupItems = computed(() =>
  ['All', ...new Set(SIGNAL_DEFS.map(s => s.group))]
);
const visibleSignals = computed(() =>
  selectedGroup.value === 'All'
    ? SIGNAL_DEFS
    : SIGNAL_DEFS.filter(s => s.group === selectedGroup.value)
);
const hasData     = computed(() => Object.keys(machineData.value).length > 0);
const onlineCount = computed(() =>
  Array.from({ length: MACHINE_COUNT }, (_, i) => i + 1)
    .filter(m => machineData.value[m] && !machineErrors.value[m]).length
);
const errorCount = computed(() =>
  Array.from({ length: MACHINE_COUNT }, (_, i) => i + 1)
    .filter(m => machineErrors.value[m]).length
);

function typeColor(dt) {
  return { bit: 'purple', int: 'blue', float: 'teal', string: 'orange' }[dt] ?? 'grey';
}
function machineStatus(m) {
  if (!hasData.value)    return 'default';
  if (machineErrors.value[m]) return 'error';
  if (machineData.value[m])   return 'success';
  return 'default';
}
function machineStatusLabel(m) {
  if (!hasData.value)    return '—';
  if (machineErrors.value[m]) return 'Error';
  if (machineData.value[m])   return 'Online';
  return '—';
}
function machineRowClass(m) {
  if (machineErrors.value[m]) return 'row-error';
  return m % 2 === 0 ? 'row-even' : 'row-odd';
}
function cellClass(m, sig) {
  return sig.dataType === 'bit' && machineData.value[m]?.[sig.address] === true
    ? 'cell-bit-on' : '';
}
function formatNumeric(val, sig) {
  if (val === null || val === undefined) return '—';
  const n = Number(val);
  if (isNaN(n)) return String(val);
  const str = sig.dataType === 'float' ? n.toFixed(2) : n.toLocaleString();
  return sig.unit ? `${str} ${sig.unit}` : str;
}

function togglePolling() { polling.value ? stopPolling() : startPolling(); }
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

watch(pollInterval, () => { if (polling.value) { stopPolling(); startPolling(); } });

async function fetchAllSignals() {
  fetching.value = true;
  try {
    const res = await fetch('/api/setting/plc-signals');
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    const all = await res.json();

    for (let m = 1; m <= MACHINE_COUNT; m++) {
      const data = all[String(m)];
      machineData.value[m]   = data ?? null;
      machineErrors.value[m] = data ? null : 'No data';
    }
    lastUpdated.value = new Date().toLocaleTimeString();
  } catch (err) {
    for (let m = 1; m <= MACHINE_COUNT; m++)
      machineErrors.value[m] = err.message;
  } finally {
    fetching.value = false;
  }
}

onUnmounted(() => stopPolling());
</script>

<style scoped>
  .signal-table {
    border-collapse: collapse;
    width: max-content;
    font-size: 12px;
  }

    .signal-table th, .signal-table td {
      padding: 4px 8px;
      border: 1px solid #e0e0e0;
      white-space: nowrap;
    }

  .col-frozen {
    position: sticky;
    background: #fff;
    z-index: 1;
  }

  .col-machine {
    left: 0;
    min-width: 60px;
  }

  .col-ip {
    left: 60px;
    min-width: 110px;
  }

  .col-status {
    left: 170px;
    min-width: 70px;
  }

  .col-signal {
    min-width: 90px;
    text-align: center;
  }

  .sig-header {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 2px;
  }

  .sig-name {
    font-weight: 600;
  }

  .sig-addr {
    font-family: monospace;
    color: #666;
    font-size: 10px;
  }

  .row-even {
    background: #fafafa;
  }

  .row-odd {
    background: #fff;
  }

  .row-error {
    background: #fff3f3;
  }

  .cell-bit-on {
    background: #e8f5e9;
  }

  .cell-numeric {
    font-family: monospace;
  }

  .cell-string {
    font-family: monospace;
    max-width: 120px;
    overflow: hidden;
    text-overflow: ellipsis;
    display: block;
  }
</style>
