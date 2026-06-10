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
      <v-chip v-if="offlineCount > 0" color="error" size="small" variant="tonal" prepend-icon="mdi-alert-circle-outline">
        {{ offlineCount }} Offline
      </v-chip>
      <v-chip v-if="pendingCount > 0" color="warning" size="small" variant="tonal" prepend-icon="mdi-timer-sand">
        {{ pendingCount }} Pending
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

      <!-- Row slot: handles offline, pending, and normal rows -->
      <template v-slot:item="{ item, columns, props: rowProps }">

        <!-- Pending row (not yet fetched) -->
        <tr v-if="item.isPending" v-bind="rowProps" class="pending-row">
          <td :colspan="columns.length" class="text-left pa-2">
            <span class="text-caption text-medium-emphasis font-italic">
              <v-icon size="14" class="mr-1">mdi-timer-sand</v-icon>
              {{ item.name }} — Waiting for data…
            </span>
          </td>
        </tr>

        <!-- Offline row -->
        <tr v-else-if="!item.online" v-bind="rowProps">
          <td v-for="col in columns" :key="col.key" class="text-caption pa-1">
            <template v-if="col.key === 'id'">
              {{ item.id }}
            </template>
            <template v-else-if="col.key === 'name'">
              {{ item.name }}
            </template>
            <template v-else-if="col.key === 'ip'">
              <span class="text-monospace">{{ item.ip }}</span>
            </template>
            <template v-else-if="col.key === 'status'">
              <v-chip color="error" size="x-small" variant="flat">Offline</v-chip>
            </template>
            <template v-else>
              <span class="text-disabled">—</span>
            </template>
          </td>
        </tr>

        <!-- Online row -->
        <tr v-else v-bind="rowProps">
          <td v-for="col in columns" :key="col.key" :style="col.nowrap ? 'white-space:nowrap' : ''">
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
              <v-chip color="success" size="x-small" variant="flat">Online</v-chip>
            </template>
            <template v-else>
              <div class="d-flex justify-center align-center">
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
              </div>
            </template>
          </td>
        </tr>

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
  const cacheState = ref({});
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
  const onlineCount  = computed(() => machines.value.filter(m => cacheState.value[m.id]?.online === true).length);
  const offlineCount = computed(() => machines.value.filter(m => cacheState.value[m.id]?.online === false).length);
  const pendingCount = computed(() => machines.value.filter(m => cacheState.value[m.id] === undefined).length);

  // --- Table headers (no fixed widths — let browser size naturally) ---
  const tableHeaders = computed(() => [
    { title: 'M#',     key: 'id',     sortable: false },
    { title: 'Machine', key: 'name',  sortable: false },
    { title: 'IP',     key: 'ip',     sortable: false },
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
      const isPending = entry === undefined;
      const online    = !isPending && entry?.online === true;

      const row = {
        id:        m.id,
        name:      m.name,
        ip:        `${MACHINE_IP_PREFIX}${220 + m.id}`,
        isPending,
        online,
      };

      if (online) {
        for (const sig of visibleSignals.value)
          row[`sig_${sig.address}`] = entry[sig.address];
      }

      return row;
    })
  );

  function getRowProps({ item }) {
    if (item.isPending)  return { class: 'bg-grey-lighten-4' };
    if (!item.online)    return { class: 'bg-red-lighten-5' };
    return {};
  }

  // --- Helpers ---

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
    const machineList = machines.value;
    if (!machineList.length) return;

    for (const machine of machineList) {
      try {
        const res = await fetch(`/api/setting/plc-signals/${machine.id}`);
        if (!res.ok) {
          cacheState.value[machine.id] = { online: false };
          continue;
        }
        const data = await res.json();
        cacheState.value[machine.id] = data;
      } catch {
        cacheState.value[machine.id] = { online: false };
      }
    }

    lastUpdated.value = new Date().toLocaleTimeString();
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
