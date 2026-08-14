<template>
  <div class="plc-monitor d-flex flex-column h-100 pa-2" style="min-height: 0;">

    <!-- ── Toolbar ─────────────────────────────────────────────────────────── -->
    <div class="d-flex align-center ga-2 mb-2 flex-shrink-0 flex-wrap">
      <v-select v-model="selectedGroup" :items="signalGroupItems" label="Signal Group" variant="outlined"
        density="compact" hide-details style="max-width: 200px;" />

      <v-btn size="small" variant="outlined" @click="resetColumnLayout" title="Reset column order & widths">
        <v-icon start size="16">mdi-table-refresh</v-icon>
        Reset Layout
      </v-btn>

      <v-spacer />

      <v-chip color="success" size="small" variant="tonal" prepend-icon="mdi-check-circle-outline">
        {{ onlineCount }} / {{ machines.length }} Online
      </v-chip>
      <v-chip v-if="offlineCount > 0" color="error" size="small" variant="tonal"
        prepend-icon="mdi-alert-circle-outline">
        {{ offlineCount }} Offline
      </v-chip>
      <v-chip v-if="pendingCount > 0" color="warning" size="small" variant="tonal" prepend-icon="mdi-timer-sand">
        {{ pendingCount }} Pending
      </v-chip>
      <span v-if="lastUpdated" class="text-caption text-medium-emphasis">
        Updated: {{ lastUpdated }}
      </span>
    </div>

    <!-- ── Table wrapper ────────────────────────────────────────────────────── -->
    <div class="plc-table-wrapper flex-grow-1 overflow-auto position-relative">
      <table class="plc-table" ref="tableRef">
        <thead>
          <tr>
            <!-- Fixed columns (always frozen) -->
            <th class="col-fixed col-id" data-col="id">#</th>
            <th class="col-fixed col-name" data-col="name">Machine</th>
            <th class="col-fixed col-ip" data-col="ip">IP</th>
            <th class="col-fixed col-status" data-col="status">Status</th>

            <!-- Draggable / resizable signal columns -->
            <th v-for="sig in orderedSignals" :key="sig.address" :data-col="sig.address"
              :style="{ width: colWidths[sig.address] ? colWidths[sig.address] + 'px' : '90px', minWidth: '60px' }"
              class="col-signal" draggable="true" @dragstart="onDragStart($event, sig.address)"
              @dragover.prevent="onDragOver($event, sig.address)" @drop="onDrop($event, sig.address)"
              @dragend="onDragEnd">
              <div class="sig-header">
                <span class="sig-name text-caption font-weight-bold">{{ sig.name }}</span>
                <v-chip :color="typeColor(sig.dataType)" size="x-small" variant="tonal" class="mt-1">
                  {{ sig.dataType }}
                </v-chip>
                <span class="sig-addr text-caption text-medium-emphasis text-monospace">{{ sig.address }}</span>
              </div>
              <!-- Resize handle -->
              <div class="resize-handle" @mousedown.prevent.stop="onResizeStart($event, sig.address)"></div>
            </th>
          </tr>
        </thead>

        <tbody>
          <template v-for="m in machines" :key="m.id">
            <tr v-if="cacheState[m.id] === undefined" class="row-pending">
              <td class="col-fixed col-id">{{ m.id }}</td>
              <td class="col-fixed col-name">{{ m.name }}</td>
              <td class="col-fixed col-ip text-monospace text-caption">{{ machineIp(m.id) }}</td>
              <td class="col-fixed col-status">
                <v-chip color="warning" size="x-small" variant="flat">
                  <v-icon start size="12">mdi-timer-sand</v-icon>Pending
                </v-chip>
              </td>
              <td :colspan="orderedSignals.length" class="text-caption text-disabled text-center pa-1">
                Waiting for data…
              </td>
            </tr>

            <tr v-else-if="cacheState[m.id]?.online === false" class="row-offline">
              <td class="col-fixed col-id">{{ m.id }}</td>
              <td class="col-fixed col-name">{{ m.name }}</td>
              <td class="col-fixed col-ip text-monospace text-caption">{{ machineIp(m.id) }}</td>
              <td class="col-fixed col-status">
                <v-chip color="error" size="x-small" variant="flat">
                  <v-icon start size="12">mdi-lan-disconnect</v-icon>Offline
                </v-chip>
              </td>
              <td v-for="sig in orderedSignals" :key="sig.address" class="text-center text-disabled">—</td>
            </tr>

            <tr v-else class="row-online">
              <td class="col-fixed col-id">{{ m.id }}</td>
              <td class="col-fixed col-name" style="white-space: nowrap;">{{ m.name }}</td>
              <td class="col-fixed col-ip text-monospace text-caption">{{ machineIp(m.id) }}</td>
              <td class="col-fixed col-status">
                <v-chip color="success" size="x-small" variant="flat">
                  <v-icon start size="12">mdi-lan-connect</v-icon>Online
                </v-chip>
              </td>
              <td v-for="sig in orderedSignals" :key="sig.address" class="text-center cell-value">
                <template v-if="sig.dataType === 'bit'">
                  <v-icon :color="cacheState[m.id]?.[sig.address] ? '#4caf50' : '#f44336'" size="16">
                    mdi-circle
                  </v-icon>
                </template>
                <template v-else-if="sig.dataType === 'string'">
                  <span class="text-monospace text-caption" style="white-space: nowrap;">
                    {{ cacheState[m.id]?.[sig.address] || '—' }}
                  </span>
                </template>
                <template v-else>
                  <span class="text-monospace text-caption">
                    {{ formatNumeric(cacheState[m.id]?.[sig.address], sig) }}
                  </span>
                </template>
              </td>
            </tr>
          </template>
        </tbody>
      </table>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted, watch } from 'vue';
import { useMachineStore } from '@/store/machineStore';
import { SIGNAL_DEFS, MACHINE_IP_PREFIX } from '@/utils/constant.js';

const STORAGE_KEY_ORDER = 'plc_col_order';
const STORAGE_KEY_WIDTHS = 'plc_col_widths';
const POLL_MS = 5000;

// The Setting page keeps every tab mounted (v-window), so onUnmounted does not
// fire on tab switch. `active` is true only while this tab is shown; polling is
// gated on it so the fetch loop stops when the user navigates away.
const props = defineProps({
  active: { type: Boolean, default: true },
});

const store = ref(null);
const storeInst = useMachineStore();
store.value = storeInst;

const selectedGroup = ref('All');
const lastUpdated = ref('');
const cacheState = ref({});
const tableRef = ref(null);

// ── Column order & widths (persisted) ─────────────────────────────────────────

const defaultOrder = SIGNAL_DEFS.map(s => s.address);

const colOrder = ref((() => {
  try {
    const saved = JSON.parse(localStorage.getItem(STORAGE_KEY_ORDER) || 'null');
    if (Array.isArray(saved) && saved.length === defaultOrder.length) return saved;
  } catch { }
  return [...defaultOrder];
})());

const colWidths = ref((() => {
  try {
    return JSON.parse(localStorage.getItem(STORAGE_KEY_WIDTHS) || '{}');
  } catch { }
  return {};
})());

watch(colOrder, v => localStorage.setItem(STORAGE_KEY_ORDER, JSON.stringify(v)), { deep: true });
watch(colWidths, v => localStorage.setItem(STORAGE_KEY_WIDTHS, JSON.stringify(v)), { deep: true });

function resetColumnLayout() {
  colOrder.value = [...defaultOrder];
  colWidths.value = {};
  localStorage.removeItem(STORAGE_KEY_ORDER);
  localStorage.removeItem(STORAGE_KEY_WIDTHS);
}

// ── Derived ────────────────────────────────────────────────────────────────────

const signalGroupItems = computed(() => ['All', ...new Set(SIGNAL_DEFS.map(s => s.group))]);

const visibleSignals = computed(() =>
  selectedGroup.value === 'All'
    ? SIGNAL_DEFS
    : SIGNAL_DEFS.filter(s => s.group === selectedGroup.value)
);

const visibleAddresses = computed(() => new Set(visibleSignals.value.map(s => s.address)));

const orderedSignals = computed(() => {
  const sigByAddr = Object.fromEntries(SIGNAL_DEFS.map(s => [s.address, s]));
  return colOrder.value
    .filter(addr => visibleAddresses.value.has(addr))
    .map(addr => sigByAddr[addr])
    .filter(Boolean);
});

const machines = computed(() =>
  storeInst.machineData
    .slice()
    .sort((a, b) => a.id_machine - b.id_machine)
    .map(m => ({ id: m.id_machine, name: m.machine_name }))
);

const onlineCount = computed(() => machines.value.filter(m => cacheState.value[m.id]?.online === true).length);
const offlineCount = computed(() => machines.value.filter(m => cacheState.value[m.id]?.online === false).length);
const pendingCount = computed(() => machines.value.filter(m => cacheState.value[m.id] === undefined).length);

function machineIp(id) { return `${MACHINE_IP_PREFIX}${220 + id}`; }

// ── Fetch ──────────────────────────────────────────────────────────────────────

async function fetchAllSignals() {
  const list = machines.value;
  if (!list.length) return;

  for (const m of list) {
    try {
      const res = await fetch(`/api/setting/plc-signals/${m.id}`);
      if (!res.ok) {
        cacheState.value[m.id] = { online: false };
        continue;
      }
      const data = await res.json();
      if (typeof data.online !== 'boolean') data.online = false;
      cacheState.value[m.id] = data;
    } catch {
      cacheState.value[m.id] = { online: false };
    }
  }

  lastUpdated.value = new Date().toLocaleTimeString();
}

// ── Column drag-to-reorder ─────────────────────────────────────────────────────

let dragSrc = null;

function onDragStart(e, address) {
  dragSrc = address;
  e.dataTransfer.effectAllowed = 'move';
  e.currentTarget.classList.add('dragging');
}

function onDragOver(e, address) {
  if (!dragSrc || dragSrc === address) return;
  e.currentTarget.classList.add('drag-over');
}

function onDrop(e, targetAddress) {
  e.currentTarget.classList.remove('drag-over');
  if (!dragSrc || dragSrc === targetAddress) return;

  const order = [...colOrder.value];
  const srcIdx = order.indexOf(dragSrc);
  const tgtIdx = order.indexOf(targetAddress);
  if (srcIdx === -1 || tgtIdx === -1) return;

  order.splice(srcIdx, 1);
  order.splice(tgtIdx, 0, dragSrc);
  colOrder.value = order;
  dragSrc = null;
}

function onDragEnd(e) {
  e.currentTarget.classList.remove('dragging');
  tableRef.value?.querySelectorAll('.drag-over').forEach(el => el.classList.remove('drag-over'));
  dragSrc = null;
}

// ── Column resize ──────────────────────────────────────────────────────────────

let resizeState = null;

function onResizeStart(e, address) {
  const th = e.currentTarget.closest('th');
  const startX = e.clientX;
  const startW = th.getBoundingClientRect().width;

  resizeState = { address, startX, startW };
  document.addEventListener('mousemove', onResizeMove);
  document.addEventListener('mouseup', onResizeEnd);
  document.body.style.cursor = 'col-resize';
  document.body.style.userSelect = 'none';
}

function onResizeMove(e) {
  if (!resizeState) return;
  const delta = e.clientX - resizeState.startX;
  const newW = Math.max(60, resizeState.startW + delta);
  colWidths.value = { ...colWidths.value, [resizeState.address]: newW };
}

function onResizeEnd() {
  resizeState = null;
  document.removeEventListener('mousemove', onResizeMove);
  document.removeEventListener('mouseup', onResizeEnd);
  document.body.style.cursor = '';
  document.body.style.userSelect = '';
}

// ── Helpers ────────────────────────────────────────────────────────────────────

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

// ── Lifecycle ──────────────────────────────────────────────────────────────────

let pollTimer = null;

function startPolling() {
  if (pollTimer) return;
  fetchAllSignals();
  pollTimer = setInterval(fetchAllSignals, POLL_MS);
}

function stopPolling() {
  if (!pollTimer) return;
  clearInterval(pollTimer);
  pollTimer = null;
}

watch(() => props.active, (isActive) => {
  if (isActive) startPolling();
  else stopPolling();
});

onMounted(async () => {
  if (storeInst.machineData.length === 0) await storeInst.loadMachineMaster();
  if (props.active) startPolling();
});

onUnmounted(() => {
  stopPolling();
  document.removeEventListener('mousemove', onResizeMove);
  document.removeEventListener('mouseup', onResizeEnd);
});
</script>

<style scoped>
.plc-monitor {
  font-size: 12px;
}

.plc-table-wrapper {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 4px;
  min-height: 0;
}

.plc-table {
  border-collapse: separate;
  border-spacing: 0;
  width: max-content;
  min-width: 500px;
}

/* ── Frozen left columns ──────────────────────────────────────────────────── */
.col-fixed {
  position: sticky;
  background: rgb(var(--v-theme-surface));
  z-index: 2;
  white-space: nowrap;
  border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-right: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  padding: 4px 8px;
}

thead .col-fixed {
  z-index: 3;
  background: rgb(var(--v-theme-surface-variant));
  font-weight: 600;
  top: 0;
}

.col-id {
  left: 0;
  width: 40px;
}

.col-name {
  left: 40px;
  min-width: 90px;
}

.col-ip {
  left: 130px;
  min-width: 110px;
}

.col-status {
  left: 240px;
  min-width: 80px;
  border-right: 2px solid rgb(var(--v-theme-primary)) !important;
}

/* Shadow to visually separate frozen columns */
.col-status::after {
  content: '';
  position: absolute;
  right: -6px;
  top: 0;
  bottom: 0;
  width: 6px;
  background: linear-gradient(to right, rgba(0, 0, 0, 0.08), transparent);
  pointer-events: none;
}

/* ── Signal columns ───────────────────────────────────────────────────────── */
thead th.col-signal {
  position: sticky;
  top: 0;
  z-index: 1;
  background: rgb(var(--v-theme-surface-variant));
  border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-right: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  padding: 4px 6px 4px 6px;
  cursor: grab;
  user-select: none;
}

thead th.col-signal:active {
  cursor: grabbing;
}

.sig-header {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 2px;
  padding-right: 8px;
  /* space for resize handle */
}

.sig-name {
  font-weight: 600;
}

.sig-addr {
  font-size: 10px;
}

/* ── Resize handle ────────────────────────────────────────────────────────── */
.resize-handle {
  position: absolute;
  right: 0;
  top: 0;
  bottom: 0;
  width: 6px;
  cursor: col-resize;
  background: transparent;
  z-index: 4;
}

.resize-handle:hover {
  background: rgba(var(--v-theme-primary), 0.3);
}

/* ── Body cells ───────────────────────────────────────────────────────────── */
tbody td {
  border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-right: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  padding: 3px 6px;
  vertical-align: middle;
}

.cell-value {
  text-align: center;
}

/* ── Row states ───────────────────────────────────────────────────────────── */
.row-pending {
  background: rgba(var(--v-theme-surface-variant), 0.4);
}

.row-offline {
  background: rgba(var(--v-theme-error), 0.06);
}

.row-online:hover {
  background: rgba(var(--v-theme-primary), 0.04);
}

/* ── Drag states ──────────────────────────────────────────────────────────── */
.dragging {
  opacity: 0.5;
}

.drag-over {
  background: rgba(var(--v-theme-primary), 0.15) !important;
}
</style>