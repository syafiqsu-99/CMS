<template>
  <div class="d-flex flex-column h-100 pa-2">

    <!-- Status bar -->
    <div class="d-flex align-center ga-2 mb-2 flex-shrink-0 flex-wrap">
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
      <v-chip v-if="readingCount > 0" color="warning" size="small" variant="tonal" prepend-icon="mdi-timer-sand">
        {{ readingCount }} Reading
      </v-chip>
      <span v-if="lastUpdated" class="text-caption text-medium-emphasis">Updated: {{ lastUpdated }}</span>
    </div>

    <!-- Table -->
    <v-data-table-virtual :headers="tableHeaders"
                          :items="tableItems"
                          height="70vh"
                          fixed-header
                          density="compact"
                          hover
                          :row-props="getRowProps"
                          class="border rounded plc-table">

      <!-- Custom column headers -->
      <template v-for="sig in visibleSignals"
                :key="'hdr_' + sig.address"
                v-slot:[`header.sig_${sig.address}`]>
        <div class="d-flex flex-column align-center justify-center py-1" style="white-space: nowrap;">
          <span class="font-weight-bold text-caption">{{ sig.name }}</span>
          <v-chip :color="typeColor(sig.dataType)" size="x-small" variant="tonal" class="mt-1">
            {{ sig.dataType }}
          </v-chip>
          <span class="text-caption text-medium-emphasis text-monospace">{{ sig.address }}</span>
        </div>
      </template>

      <!-- Status chip -->
      <template v-slot:item.status="{ item }">
        <v-chip :color="item.statusColor" size="x-small" variant="flat">
          {{ item.statusLabel }}
        </v-chip>
      </template>

      <!-- IP -->
      <template v-slot:item.ip="{ item }">
        <span class="text-caption text-monospace">{{ item.ip }}</span>
      </template>

      <!-- Signal cells -->
      <template v-for="sig in visibleSignals"
                :key="'cell_' + sig.address"
                v-slot:[`item.sig_${sig.address}`]="{ item }">
        <!-- Whole-row "Reading" is handled via row slot; individual cells show — when no data -->
        <div class="d-flex justify-center align-center">
          <template v-if="item.isReading">
            <span class="text-caption text-medium-emphasis font-italic">—</span>
          </template>
          <template v-else-if="item.hasData">
            <!-- Boolean: filled circle, green = true, red = false -->
            <template v-if="sig.dataType === 'bit'">
              <v-icon :color="item[`sig_${sig.address}`] ? '#4caf50' : '#f44336'" size="16">
                mdi-circle
              </v-icon>
            </template>
            <!-- String -->
            <template v-else-if="sig.dataType === 'string'">
              <span class="text-monospace text-caption" style="white-space: nowrap;">
                {{ item[`sig_${sig.address}`] || '—' }}
              </span>
            </template>
            <!-- Numeric -->
            <template v-else>
              <span class="text-monospace text-caption">{{ formatNumeric(item[`sig_${sig.address}`], sig) }}</span>
            </template>
          </template>
          <template v-else>
            <span class="text-disabled text-caption">—</span>
          </template>
        </div>
      </template>

      <!-- "Reading" rows: override the entire row body with a single spanning cell -->
      <template v-slot:item="{ item, columns, props: rowProps }">
        <tr v-bind="rowProps" v-if="item.isReading" class="reading-row">
          <td :colspan="columns.length" class="text-left pa-2">
            <span class="text-caption text-medium-emphasis font-italic">
              <v-icon size="14" class="mr-1">mdi-timer-sand</v-icon>
              {{ item.name }} — Reading…
            </span>
          </td>
        </tr>
        <!-- Normal row: let Vuetify render cells as usual via default slot passthrough -->
        <template v-else>
          <tr v-bind="rowProps">
            <td v-for="col in columns" :key="col.key" :style="col.nowrap ? 'white-space:nowrap' : ''">
              <!-- Fixed columns -->
              <template v-if="col.key === 'id'">
                {{ item.id }}
              </template>
              <template v-else-if="col.key === 'name'">
                <span style="white-space: nowrap;">{{ item.name }}</span>
              </template>
              <template v-else-if="col.key === 'ip'">
                <span class="text-caption text-monospace">{{ item.ip }}</span>
              </template>
              <template v-else-if="col.key === 'status'">
                <v-chip :color="item.statusColor" size="x-small" variant="flat">{{ item.statusLabel }}</v-chip>
              </template>
              <!-- Signal columns -->
              <template v-else>
                <div class="d-flex justify-center align-center">
                  <template v-if="item.hasData">
                    <template v-if="signalByKey[col.key]?.dataType === 'bit'">
                      <v-icon :color="item[col.key] ? '#4caf50' : '#f44336'" size="16">mdi-circle</v-icon>
                    </template>
                    <template v-else-if="signalByKey[col.key]?.dataType === 'string'">
                      <span class="text-monospace text-caption" style="white-space: nowrap;">
                        {{ item[col.key] || '—' }}
                      </span>
                    </template>
                    <template v-else>
                      <span class="text-monospace text-caption">
                        {{ formatNumeric(item[col.key], signalByKey[col.key]) }}
                      </span>
                    </template>
                  </template>
                  <template v-else>
                    <span class="text-disabled text-caption">—</span>
                  </template>
                </div>
              </template>
            </td>
          </tr>
        </template>
      </template>

    </v-data-table-virtual>

  </div>
</template>

<script setup>
  import { ref, computed, onMounted, onUnmounted } from 'vue';
  import { useMachineStore } from '@/store/machineStore';
  import { SIGNAL_DEFS, MACHINE_IP_PREFIX } from '@/utils/constant.js';

  const store = useMachineStore();

  const selectedGroup = ref('All');
  const lastUpdated = ref('');
  const cacheState = ref({});   // id -> null (Reading) | { online: bool, data: {} }
  let pollTimer = null;
  const POLL_MS = 5000;

  // --- Signal group filter ---
  const signalGroupItems = computed(() => ['All', ...new Set(SIGNAL_DEFS.map(s => s.group))]);

  const visibleSignals = computed(() =>
    selectedGroup.value === 'All'
      ? SIGNAL_DEFS
      : SIGNAL_DEFS.filter(s => s.group === selectedGroup.value)
  );

  // Lookup map: `sig_${address}` -> signal def, used in the row slot
  const signalByKey = computed(() => {
    const map = {};
    for (const sig of visibleSignals.value)
      map[`sig_${sig.address}`] = sig;
    return map;
  });

  // --- Machine list ---
  const machines = computed(() =>
    store.machineData
      .sort((a, b) => a.id_machine - b.id_machine)
      .map(m => ({ id: m.id_machine, name: m.machine_name }))
  );

  // --- Summary counts ---
  const onlineCount = computed(() => machines.value.filter(m => cacheState.value[m.id]?.online === true).length);
  const errorCount = computed(() => machines.value.filter(m => { const s = cacheState.value[m.id]; return s !== undefined && s !== null && s.online === false; }).length);
  const readingCount = computed(() => machines.value.filter(m => cacheState.value[m.id] == null).length);

  // --- Table headers (no fixed widths — let browser size naturally) ---
  const tableHeaders = computed(() => [
    { title: 'M#', key: 'id', sortable: false },
    { title: 'Machine', key: 'name', sortable: false },
    { title: 'IP', key: 'ip', sortable: false },
    { title: 'Status', key: 'status', sortable: false },
    ...visibleSignals.value.map(sig => ({
      title: sig.name,
      key: `sig_${sig.address}`,
      align: 'center',
      sortable: false,
    })),
  ]);

  // --- Table rows ---
  const tableItems = computed(() =>
    machines.value.map(m => {
      const entry = cacheState.value[m.id];
      const isReading = entry == null;
      const hasData = !!entry?.data;

      const row = {
        id: m.id,
        name: m.name,
        ip: `${MACHINE_IP_PREFIX}${219 + m.id}`,
        statusLabel: resolveStatusLabel(entry),
        statusColor: resolveStatusColor(entry),
        isReading,
        hasData,
      };

      if (hasData) {
        for (const sig of visibleSignals.value)
          row[`sig_${sig.address}`] = entry.data[sig.address];
      }

      return row;
    })
  );

  function getRowProps({ item }) {
    if (item.isReading) return { class: 'bg-grey-lighten-4' };
    if (cacheState.value[item.id]?.online === false) return { class: 'bg-red-lighten-5' };
    return {};
  }

  // --- Helpers ---
  function resolveStatusLabel(entry) {
    if (entry == null) return 'Reading';
    return entry.online ? 'Online' : 'Error';
  }

  function resolveStatusColor(entry) {
    if (entry == null) return 'warning';
    return entry.online ? 'success' : 'error';
  }

  function typeColor(dt) {
    return { bit: 'purple', int: 'blue', float: 'teal', string: 'orange' }[dt] ?? 'grey';
  }

  function formatNumeric(val, sig) {
    if (val === null || val === undefined) return '—';
    const n = Number(val);
    if (isNaN(n)) return String(val);
    const str = sig?.dataType === 'float' ? n.toFixed(2) : n.toLocaleString();
    return sig?.unit ? `${str} ${sig.unit}` : str;
  }

  // --- Fetch cache snapshot ---
  async function fetchAllSignals() {
    try {
      const res = await fetch('/api/setting/plc-signals');
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const payload = await res.json();

      for (const [key, entry] of Object.entries(payload)) {
        const id = Number(key);
        if (entry === null || entry === undefined) {
          if (!(id in cacheState.value)) cacheState.value[id] = null;
        } else {
          cacheState.value[id] = entry;
        }
      }

      lastUpdated.value = new Date().toLocaleTimeString();
    } catch (err) {
      console.error('[PlcSignalMonitor] fetch error:', err.message);
    }
  }

  // --- Lifecycle: auto-start polling on mount ---
  onMounted(async () => {
    if (store.machineData.length === 0) await store.loadMachineMaster();
    fetchAllSignals();
    pollTimer = setInterval(fetchAllSignals, POLL_MS);
  });

  onUnmounted(() => {
    clearInterval(pollTimer);
    pollTimer = null;
  });
</script>

<style scoped>
  .plc-table :deep(table) {
    table-layout: auto;
  }

  .reading-row td {
    background-color: rgb(var(--v-theme-surface-variant), 0.3);
  }
</style>
