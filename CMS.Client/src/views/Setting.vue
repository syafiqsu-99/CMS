<template>
  <v-container fluid class="pa-2 h-100 d-flex flex-column" style="overflow: hidden;">

    <!-- ── Page Header ─────────────────────────────────────────────────────── -->
    <v-card elevation="2" class="mb-3 flex-shrink-0">
      <v-card-title class="d-flex align-center ga-2 py-2 px-3">
        <v-icon color="primary">mdi-cog-outline</v-icon>
        <span class="text-subtitle-1 font-weight-bold">System Settings</span>
      </v-card-title>
    </v-card>

    <!-- ── Tabs ────────────────────────────────────────────────────────────── -->
    <v-card elevation="2" class="d-flex flex-column flex-grow-1" style="overflow: hidden; min-height: 0;">
      <v-tabs v-model="activeTab" color="primary" density="compact" class="flex-shrink-0">
        <v-tab value="password">
          <v-icon start size="16">mdi-lock-reset</v-icon>
          PLC Passwords
        </v-tab>
        <v-tab value="signals">
          <v-icon start size="16">mdi-access-point</v-icon>
          Live Signal Monitor
        </v-tab>
      </v-tabs>
      <v-divider />

      <v-window v-model="activeTab" class="flex-grow-1" style="overflow: hidden; min-height: 0;">

        <!-- ══ Tab 1: Department Passwords ═══════════════════════════════════ -->
        <v-window-item value="password" class="h-100" style="overflow-y: auto;">
          <v-container fluid class="pa-4">
            <v-row>
              <v-col cols="12" md="6" lg="5">
                <v-card elevation="1" border>
                  <v-card-title class="d-flex align-center ga-2 py-2 px-3">
                    <v-icon color="primary" size="20">mdi-lock-reset</v-icon>
                    <span class="text-subtitle-2 font-weight-bold">Department PLC Passwords</span>
                  </v-card-title>
                  <v-divider />
                  <v-card-text class="pt-4">
                    <p class="text-caption text-medium-emphasis mb-4">
                      Passwords are stored as DINT values in the main PLC Holding area
                      (H10 / H12 / H14) and propagated to all sub-PLCs via the W1.01 trigger signal.
                    </p>
                    <div v-for="dept in departments" :key="dept.key" class="mb-3">
                      <div class="d-flex align-center ga-2">
                        <v-icon :color="dept.color" size="18">{{ dept.icon }}</v-icon>
                        <span class="text-body-2 font-weight-medium" style="min-width: 110px;">
                          {{ dept.label }}
                        </span>
                        <v-text-field v-model.number="passwordForm[dept.key]"
                                      :label="`H${dept.holdingAddr} (DINT)`"
                                      type="number" min="0"
                                      variant="outlined" density="compact"
                                      hide-details class="flex-grow-1"
                                      :disabled="saving" />
                      </div>
                    </div>
                    <v-checkbox v-for="dept in departments" :key="`chk-${dept.key}`"
                                v-model="selectedDepts" :value="dept.key"
                                :label="`Update ${dept.label}`"
                                density="compact" hide-details class="mt-1"
                                :disabled="saving" />
                  </v-card-text>
                  <v-divider />
                  <v-card-actions class="pa-3">
                    <span v-if="passwordStatus" class="text-caption" :class="passwordStatusColor">
                      {{ passwordStatus }}
                    </span>
                    <v-spacer />
                    <v-btn variant="outlined" size="small" :disabled="saving" @click="resetPasswordForm">Reset</v-btn>
                    <v-btn color="primary" variant="flat" size="small" :loading="saving"
                           :disabled="selectedDepts.length === 0" @click="confirmPasswordDialog = true">
                      <v-icon start size="16">mdi-send</v-icon>
                      Apply to PLC
                    </v-btn>
                  </v-card-actions>
                </v-card>
              </v-col>
            </v-row>
          </v-container>
        </v-window-item>

        <!-- ══ Tab 2: Live Signal Monitor ════════════════════════════════════ -->
        <v-window-item value="signals" class="d-flex flex-column h-100" style="overflow: hidden;">

          <!-- Toolbar -->
          <div class="d-flex align-center flex-wrap ga-2 pa-3 flex-shrink-0"
               style="border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));">

            <!-- Group filter -->
            <v-select v-model="selectedGroup"
                      :items="signalGroupItems"
                      label="Signal Group"
                      variant="outlined" density="compact"
                      hide-details style="max-width: 200px;" />

            <v-spacer />

            <!-- Status summary chips -->
            <v-chip color="success" size="small" variant="tonal" prepend-icon="mdi-check-circle-outline">
              {{ onlineCount }} / 26 Online
            </v-chip>
            <v-chip v-if="errorCount > 0" color="error" size="small" variant="tonal"
                    prepend-icon="mdi-alert-circle-outline">
              {{ errorCount }} Error
            </v-chip>

            <span v-if="lastUpdated" class="text-caption text-medium-emphasis">
              Updated: {{ lastUpdated }}
            </span>

            <!-- Interval -->
            <v-select v-model="pollInterval"
                      :items="[{ title: '1s', value: 1000 }, { title: '2s', value: 2000 }, { title: '5s', value: 5000 }]"
                      item-title="title" item-value="value"
                      label="Interval" variant="outlined" density="compact"
                      hide-details style="max-width: 95px;" />

            <v-btn :color="polling ? 'error' : 'success'"
                   :prepend-icon="polling ? 'mdi-pause' : 'mdi-play'"
                   variant="flat" size="small" @click="togglePolling">
              {{ polling ? 'Pause' : 'Start' }}
            </v-btn>

            <v-btn color="primary" prepend-icon="mdi-refresh"
                   variant="outlined" size="small"
                   :loading="fetching" @click="fetchAllSignals">
              Refresh
            </v-btn>
          </div>

          <!-- Scrollable table area — both axes -->
          <div class="flex-grow-1" style="overflow: auto; min-height: 0;">
            <table class="signal-table">
              <thead>
                <tr>
                  <!-- Frozen first column: Machine -->
                  <th class="col-frozen col-machine">Machine</th>
                  <th class="col-frozen col-ip">IP</th>
                  <th class="col-frozen col-status">Status</th>
                  <!-- Dynamic signal columns for selected group -->
                  <th v-for="sig in visibleSignals" :key="sig.address" class="col-signal">
                    <div class="sig-header">
                      <span class="sig-name">{{ sig.name }}</span>
                      <v-chip :color="typeColor(sig.dataType)" size="x-small" variant="tonal"
                              class="sig-type-chip">{{ sig.dataType }}</v-chip>
                      <span class="sig-addr">{{ sig.address }}</span>
                    </div>
                  </th>
                </tr>
              </thead>
              <tbody>
                <template v-if="fetching && !hasData">
                  <tr v-for="n in 26" :key="n" class="skeleton-row">
                    <td class="col-frozen col-machine">
                      <v-skeleton-loader type="text" width="60" />
                    </td>
                    <td class="col-frozen col-ip">
                      <v-skeleton-loader type="text" width="110" />
                    </td>
                    <td class="col-frozen col-status">
                      <v-skeleton-loader type="chip" width="60" />
                    </td>
                    <td v-for="sig in visibleSignals" :key="sig.address">
                      <v-skeleton-loader type="text" width="50" />
                    </td>
                  </tr>
                </template>
                <template v-else>
                  <tr v-for="m in 26" :key="m"
                      :class="['machine-row', machineRowClass(m)]">
                    <!-- Machine label -->
                    <td class="col-frozen col-machine font-weight-bold text-body-2">
                      M{{ m }}
                    </td>
                    <!-- IP -->
                    <td class="col-frozen col-ip text-caption" style="font-family: monospace;">
                      172.17.86.{{ 219 + m }}
                    </td>
                    <!-- Status -->
                    <td class="col-frozen col-status">
                      <v-chip :color="machineStatus(m)"
                              size="x-small" variant="flat">
                        {{ machineStatusLabel(m) }}
                      </v-chip>
                    </td>
                    <!-- Signal values -->
                    <td v-for="sig in visibleSignals" :key="sig.address"
                        :class="cellClass(m, sig)">
                      <template v-if="machineData[m]">
                        <!-- Bit -->
                        <template v-if="sig.dataType === 'bit'">
                          <v-icon :color="machineData[m][sig.address] ? 'success' : 'error'"
                                  size="16">
                            {{ machineData[m][sig.address] ? 'mdi-circle' : 'mdi-circle-outline' }}
                          </v-icon>
                        </template>
                        <!-- String -->
                        <template v-else-if="sig.dataType === 'string'">
                          <span class="cell-string">
                            {{ machineData[m][sig.address] || '—' }}
                          </span>
                        </template>
                        <!-- Numeric -->
                        <template v-else>
                          <span class="cell-numeric">
                            {{ formatNumeric(machineData[m][sig.address], sig) }}
                          </span>
                        </template>
                      </template>
                      <!-- Error or loading state for this machine -->
                      <template v-else>
                        <span class="text-disabled text-caption">—</span>
                      </template>
                    </td>
                  </tr>
                </template>
              </tbody>
            </table>
          </div>

        </v-window-item>
      </v-window>
    </v-card>

    <!-- ── Confirm Password Dialog ─────────────────────────────────────────── -->
    <v-dialog v-model="confirmPasswordDialog" max-width="400px">
      <v-card>
        <v-card-title class="text-subtitle-1 font-weight-bold py-3 px-4 d-flex align-center ga-2">
          <v-icon color="warning">mdi-alert-outline</v-icon>
          Confirm Password Update
        </v-card-title>
        <v-divider />
        <v-card-text class="pt-4">
          <p class="text-body-2 mb-3">
            The following departments will have their PLC passwords updated immediately:
          </p>
          <v-chip v-for="key in selectedDepts" :key="key"
                  :color="getDept(key)?.color" size="small" class="mr-1 mb-1">
            {{ getDept(key)?.label }} → {{ passwordForm[key] }}
          </v-chip>
          <p class="text-caption text-medium-emphasis mt-3">
            This will write to the main PLC Holding area and trigger W1.01.
            Ensure no active jobs are running before proceeding.
          </p>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-3">
          <v-spacer />
          <v-btn variant="text" @click="confirmPasswordDialog = false">Cancel</v-btn>
          <v-btn color="warning" variant="flat" :loading="saving" @click="applyPasswords">
            Confirm & Write to PLC
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

  </v-container>
</template>

<script setup>
  import { ref, computed, onUnmounted, watch } from 'vue';

  // ── Tab state ─────────────────────────────────────────────────────────────────
  const activeTab = ref('password');

  // ══════════════════════════════════════════════════════════════════════════════
  // TAB 1 — PASSWORD LOGIC
  // ══════════════════════════════════════════════════════════════════════════════
  const departments = [
    { key: 'maintenance', label: 'Maintenance', holdingAddr: 10, color: 'blue', icon: 'mdi-wrench-outline' },
    { key: 'technician', label: 'Technician', holdingAddr: 12, color: 'orange', icon: 'mdi-account-hard-hat-outline' },
    { key: 'production', label: 'Production', holdingAddr: 14, color: 'green', icon: 'mdi-factory' },
  ];
  function getDept(key) { return departments.find(d => d.key === key); }

  const defaultPasswords = () => Object.fromEntries(departments.map(d => [d.key, 0]));
  const passwordForm = ref(defaultPasswords());
  const selectedDepts = ref(departments.map(d => d.key));
  const saving = ref(false);
  const passwordStatus = ref('');
  const passwordStatusColor = ref('text-success');
  const confirmPasswordDialog = ref(false);

  function resetPasswordForm() {
    passwordForm.value = defaultPasswords();
    selectedDepts.value = departments.map(d => d.key);
    passwordStatus.value = '';
  }

  async function applyPasswords() {
    saving.value = true;
    confirmPasswordDialog.value = false;
    passwordStatus.value = '';
    const payload = Object.fromEntries(
      selectedDepts.value.map(key => [key, passwordForm.value[key]])
    );
    try {
      const res = await fetch('/api/MachineLog/Settings/Password', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      });
      if (!res.ok) {
        const err = await res.json().catch(() => ({ error: res.statusText }));
        throw new Error(err.error ?? 'Unknown error');
      }
      passwordStatus.value = `✓ Passwords applied to PLC at ${new Date().toLocaleTimeString()}`;
      passwordStatusColor.value = 'text-success';
    } catch (err) {
      passwordStatus.value = `✗ Failed: ${err.message}`;
      passwordStatusColor.value = 'text-error';
    } finally {
      saving.value = false;
    }
  }

  // ══════════════════════════════════════════════════════════════════════════════
  // TAB 2 — LIVE SIGNAL MONITOR (all 26 machines × all signals)
  // ══════════════════════════════════════════════════════════════════════════════

  const SIGNAL_DEFS = [
    // Production
    { name: 'Type', dataType: 'string', address: 'D200', group: 'Production' },
    { name: 'Packer', dataType: 'string', address: 'D300', group: 'Production' },
    { name: 'Part Weight', dataType: 'float', address: 'D30', group: 'Production', unit: 'kg' },
    { name: 'Shot', dataType: 'int', address: 'D40', group: 'Production' },
    { name: 'Shot Accum', dataType: 'int', address: 'D50', group: 'Production' },
    { name: 'Act CT', dataType: 'float', address: 'D90', group: 'Production', unit: 's' },
    { name: 'HMI Page', dataType: 'int', address: 'D190', group: 'Production' },
    // Stop & Category
    { name: 'Stop Cat (Code)', dataType: 'int', address: 'D110', group: 'Stop & Category' },
    { name: 'Mould Cat', dataType: 'int', address: 'D120', group: 'Stop & Category' },
    { name: 'Stop Cat (Text)', dataType: 'string', address: 'D400', group: 'Stop & Category' },
    { name: 'Remark', dataType: 'string', address: 'D500', group: 'Stop & Category' },
    // W Signals
    { name: 'Change Shift', dataType: 'bit', address: 'W5.00', group: 'W Signals' },
    { name: 'Remark Sig', dataType: 'bit', address: 'W6.00', group: 'W Signals' },
    { name: 'Reject Sig', dataType: 'bit', address: 'W7.00', group: 'W Signals' },
    { name: 'Stop Cat Sig', dataType: 'bit', address: 'W8.00', group: 'W Signals' },
    { name: 'Visual QC', dataType: 'bit', address: 'W9.00', group: 'W Signals' },
    // Utilities
    { name: 'Barrel', dataType: 'bit', address: 'W60.00', group: 'Utilities' },
    { name: 'Hyd. Motor', dataType: 'bit', address: 'W61.00', group: 'Utilities' },
    { name: 'Dehum.', dataType: 'bit', address: 'W62.00', group: 'Utilities' },
    { name: 'Dehum. Sw.', dataType: 'bit', address: 'W63.00', group: 'Utilities' },
    { name: 'Chiller', dataType: 'bit', address: 'W63.01', group: 'Utilities' },
    { name: 'Material', dataType: 'bit', address: 'W64.00', group: 'Utilities' },
    { name: 'Dry Cycle', dataType: 'bit', address: 'W65.00', group: 'Utilities' },
    // I/O Signals
    { name: 'Power', dataType: 'bit', address: 'IN0.00', group: 'I/O Signals' },
    { name: 'Start Auto', dataType: 'bit', address: 'IN0.01', group: 'I/O Signals' },
    { name: 'Barrel DC', dataType: 'bit', address: 'IN0.02', group: 'I/O Signals' },
    { name: 'Shot Sig', dataType: 'bit', address: 'IN0.03', group: 'I/O Signals' },
    { name: 'Barrel Htr', dataType: 'bit', address: 'IN0.04', group: 'I/O Signals' },
    { name: 'Hyd. Motor IN', dataType: 'bit', address: 'IN0.05', group: 'I/O Signals' },
    { name: 'Dehum. IN', dataType: 'bit', address: 'IN0.06', group: 'I/O Signals' },
    { name: 'Chiller IN', dataType: 'bit', address: 'IN0.07', group: 'I/O Signals' },
    { name: 'Material IN', dataType: 'bit', address: 'IN0.08', group: 'I/O Signals' },
    { name: 'Alarm', dataType: 'bit', address: 'OUT100.00', group: 'I/O Signals' },
    // Passwords
    { name: 'PROD Pass', dataType: 'int', address: 'H30', group: 'Passwords' },
    { name: 'TECH Pass', dataType: 'int', address: 'H32', group: 'Passwords' },
    { name: 'MAIN Pass', dataType: 'int', address: 'H34', group: 'Passwords' },
    // Reject kg
    { name: 'Panel. kg', dataType: 'float', address: 'D740', group: 'Reject (kg)', unit: 'kg' },
    { name: 'Lumpy kg', dataType: 'float', address: 'D745', group: 'Reject (kg)', unit: 'kg' },
    { name: 'Blk.Dot kg', dataType: 'float', address: 'D750', group: 'Reject (kg)', unit: 'kg' },
    { name: 'Burst kg', dataType: 'float', address: 'D755', group: 'Reject (kg)', unit: 'kg' },
    { name: 'StartUp kg', dataType: 'float', address: 'D760', group: 'Reject (kg)', unit: 'kg' },
    { name: 'Preform kg', dataType: 'float', address: 'D765', group: 'Reject (kg)', unit: 'kg' },
    { name: 'Purging kg', dataType: 'float', address: 'D770', group: 'Reject (kg)', unit: 'kg' },
    { name: 'Others kg', dataType: 'float', address: 'D775', group: 'Reject (kg)', unit: 'kg' },
    // Reject pcs
    { name: 'Panel. pcs', dataType: 'float', address: 'D700', group: 'Reject (pcs)', unit: 'pcs' },
    { name: 'Lumpy pcs', dataType: 'float', address: 'D705', group: 'Reject (pcs)', unit: 'pcs' },
    { name: 'Blk.Dot pcs', dataType: 'float', address: 'D710', group: 'Reject (pcs)', unit: 'pcs' },
    { name: 'Burst pcs', dataType: 'float', address: 'D715', group: 'Reject (pcs)', unit: 'pcs' },
    { name: 'StartUp pcs', dataType: 'float', address: 'D720', group: 'Reject (pcs)', unit: 'pcs' },
    { name: 'Preform pcs', dataType: 'float', address: 'D725', group: 'Reject (pcs)', unit: 'pcs' },
    { name: 'Purging pcs', dataType: 'float', address: 'D730', group: 'Reject (pcs)', unit: 'pcs' },
    { name: 'Others pcs', dataType: 'float', address: 'D735', group: 'Reject (pcs)', unit: 'pcs' },
  ];

  const signalGroupItems = ['All', ...new Set(SIGNAL_DEFS.map(s => s.group))];

  // State
  const selectedGroup = ref('All');
  const polling = ref(false);
  const fetching = ref(false);
  const pollInterval = ref(2000);
  const lastUpdated = ref('');

  // machineData[machineId] = Record<address, value> | null (null = error/not loaded)
  // machineErrors[machineId] = error message string | null
  const machineData = ref({});  // { 1: {...}, 2: {...}, ... }
  const machineErrors = ref({});  // { 1: null, 2: 'timeout', ... }

  let pollTimer = null;

  // Computed
  const visibleSignals = computed(() =>
    selectedGroup.value === 'All'
      ? SIGNAL_DEFS
      : SIGNAL_DEFS.filter(s => s.group === selectedGroup.value)
  );

  const hasData = computed(() => Object.keys(machineData.value).length > 0);

  const onlineCount = computed(() =>
    Array.from({ length: 26 }, (_, i) => i + 1)
      .filter(m => machineData.value[m] && !machineErrors.value[m]).length
  );

  const errorCount = computed(() =>
    Array.from({ length: 26 }, (_, i) => i + 1)
      .filter(m => machineErrors.value[m]).length
  );

  // Polling
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
  watch(pollInterval, () => { if (polling.value) { stopPolling(); startPolling(); } });

  // Fetch all 26 machines in one request — backend runs them in parallel
  async function fetchAllSignals() {
    fetching.value = true;
    try {
      const res = await fetch('/api/MachineLog/Settings/PlcSignalsAll');
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const all = await res.json(); // { "1": {...}, "2": null, ... }

      for (let m = 1; m <= 26; m++) {
        const key = String(m);
        const data = all[key];
        if (data) {
          machineData.value[m] = data;
          machineErrors.value[m] = null;
        } else {
          machineData.value[m] = null;
          machineErrors.value[m] = 'No data';
        }
      }
      lastUpdated.value = new Date().toLocaleTimeString();
    } catch (err) {
      console.error('[SignalMonitor] bulk fetch failed:', err);
      // Mark all as errored so the table shows the error state
      for (let m = 1; m <= 26; m++) {
        machineErrors.value[m] = err.message;
      }
    } finally {
      fetching.value = false;
    }
  }

  onUnmounted(() => stopPolling());

  // Row / cell helpers
  function machineStatus(m) {
    if (!hasData.value) return 'default';
    if (machineErrors.value[m]) return 'error';
    if (machineData.value[m]) return 'success';
    return 'default';
  }
  function machineStatusLabel(m) {
    if (!hasData.value) return '—';
    if (machineErrors.value[m]) return 'Error';
    if (machineData.value[m]) return 'Online';
    return '—';
  }
  function machineRowClass(m) {
    if (machineErrors.value[m]) return 'row-error';
    return m % 2 === 0 ? 'row-even' : 'row-odd';
  }
  function cellClass(m, sig) {
    if (sig.dataType === 'bit' && machineData.value[m]?.[sig.address] === true)
      return 'cell-bit-on';
    return '';
  }

  function formatNumeric(val, sig) {
    if (val === null || val === undefined) return '—';
    const n = Number(val);
    if (isNaN(n)) return String(val);
    const str = sig.dataType === 'float' ? n.toFixed(2) : n.toLocaleString();
    return sig.unit ? `${str} ${sig.unit}` : str;
  }
  function typeColor(t) {
    return { bit: 'teal', float: 'blue', int: 'indigo', string: 'purple' }[t] ?? 'grey';
  }
</script>

<style scoped>
  /* ── Signal table — sticky header + sticky first 3 columns ──────────────── */
  .signal-table {
    border-collapse: separate;
    border-spacing: 0;
    white-space: nowrap;
    font-size: 12px;
    width: max-content;
    min-width: 100%;
  }

    /* Header row */
    .signal-table thead tr th {
      position: sticky;
      top: 0;
      z-index: 3;
      background: rgb(var(--v-theme-surface));
      border-bottom: 2px solid rgba(var(--v-border-color), 0.3);
      padding: 6px 10px;
      text-align: left;
      font-size: 11px;
      font-weight: 600;
      color: rgba(var(--v-theme-on-surface), 0.7);
    }

  /* Frozen columns — also sticky horizontally */
  .col-frozen {
    position: sticky !important;
    z-index: 4 !important;
    background: rgb(var(--v-theme-surface)) !important;
    border-right: 1px solid rgba(var(--v-border-color), 0.2);
  }

  .col-machine {
    left: 0;
    min-width: 48px;
    max-width: 48px;
  }

  .col-ip {
    left: 48px;
    min-width: 130px;
    max-width: 130px;
  }

  .col-status {
    left: 178px;
    min-width: 72px;
    max-width: 72px;
    border-right: 2px solid rgba(var(--v-border-color), 0.3) !important;
  }

  /* Signal columns */
  .col-signal {
    min-width: 90px;
    max-width: 110px;
  }

  /* Signal header cell layout */
  .sig-header {
    display: flex;
    flex-direction: column;
    gap: 2px;
    align-items: flex-start;
  }

  .sig-name {
    font-size: 11px;
    font-weight: 600;
    line-height: 1.2;
    white-space: normal;
    max-width: 90px;
  }

  .sig-addr {
    font-size: 10px;
    font-family: monospace;
    color: rgba(var(--v-theme-on-surface), 0.5);
  }

  .sig-type-chip {
    font-size: 9px !important;
    height: 14px !important;
  }

  /* Body cells */
  .signal-table tbody td {
    padding: 4px 10px;
    border-bottom: 1px solid rgba(var(--v-border-color), 0.12);
    vertical-align: middle;
  }

  /* Frozen body cells need their own background to cover scroll content */
  .signal-table tbody .col-frozen {
    background: rgb(var(--v-theme-surface)) !important;
  }

  /* Row striping */
  .row-odd {
    background: transparent;
  }

  .row-even {
    background: rgba(var(--v-theme-on-surface), 0.02);
  }

  .signal-table tbody .row-even .col-frozen {
    background: color-mix(in srgb, rgb(var(--v-theme-surface)) 97%, rgb(var(--v-theme-on-surface)) 3%) !important;
  }

  /* Error row */
  .row-error {
    background: rgba(var(--v-theme-error), 0.05) !important;
  }

  .signal-table tbody .row-error .col-frozen {
    background: color-mix(in srgb, rgb(var(--v-theme-surface)) 95%, rgb(var(--v-theme-error)) 5%) !important;
  }

  /* ON bit cell highlight */
  .cell-bit-on {
    background: rgba(76, 175, 80, 0.08) !important;
  }

  .signal-table tbody .cell-bit-on.col-frozen {
    background: rgba(76, 175, 80, 0.12) !important;
  }

  .cell-string {
    font-family: monospace;
    font-size: 11px;
    max-width: 100px;
    display: inline-block;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    vertical-align: middle;
  }

  .cell-numeric {
    font-variant-numeric: tabular-nums;
    font-size: 12px;
  }

  .machine-row:hover td {
    background: rgba(var(--v-theme-primary), 0.04) !important;
  }

  .machine-row:hover .col-frozen {
    background: color-mix(in srgb, rgb(var(--v-theme-surface)) 94%, rgb(var(--v-theme-primary)) 6%) !important;
  }
</style>
