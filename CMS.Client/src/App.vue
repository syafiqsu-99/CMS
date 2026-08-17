<template>
  <v-app :class="{ 'maintenance-offset': maintenanceActive }">
    <div v-if="maintenanceActive" class="maintenance-banner">
      <v-icon size="20" class="mr-2">mdi-alert</v-icon>
      <span>
        System update in <strong>{{ countdownDisplay }}</strong>. Please save your work — the system will restart
        briefly.
      </span>
    </div>

    <!-- Loading overlay -->
    <v-overlay v-model="isLoading" persistent class="align-center justify-center loading-overlay" style="z-index: 9999">
      <div class="text-center">
        <v-progress-circular indeterminate size="80" width="8" color="white" />
        <div class="mt-6 text-h5 font-weight-bold text-white">{{ loadingMessage }}</div>
        <div v-if="retryCount > 0" class="mt-3 text-body-1 text-white">
          Retry attempt: {{ retryCount }}
        </div>
      </div>
    </v-overlay>

    <template v-if="!isLoading">
      <NavBar />
    </template>
  </v-app>

  <!-- Global snackbar -->
  <v-snackbar v-model="snackbar.show" :color="snackbar.color" :timeout="3000">
    {{ snackbar.message }}
    <template #actions>
      <v-btn variant="text" @click="snackbar.show = false">Close</v-btn>
    </template>
  </v-snackbar>
</template>

<script setup>
import { ref, computed, provide, onMounted, onUnmounted } from 'vue';
import NavBar from '@/components/NavBar.vue';
import { useMachineStore } from '@/store/machineStore';
import loadingBg from '@/assets/jjbackground.png';

const store = useMachineStore();

const isLoading = ref(true);
const loadingMessage = ref('Connecting to server...');
const retryCount = ref(0);
const MAX_RETRIES = 10;
const RETRY_DELAY = 2000;

const MAINTENANCE_POLL_INTERVAL = 5000;

// ── Snackbar (global via provide) ─────────────────────────────────────────────

const snackbar = ref({ show: false, message: '', color: 'success' });

function showSnackbar(message, color = 'success') {
  snackbar.value = { show: true, message, color };
}
provide('showSnackbar', showSnackbar);

// ── Backend health check ──────────────────────────────────────────────────────

async function checkHealth() {
  try {
    const res = await fetch('/api/base/Health');
    if (!res.ok) return false;
    const data = await res.json();
    return data.status === 'Ready';
  } catch {
    return false;
  }
}

async function waitForBackend() {
  while (retryCount.value < MAX_RETRIES) {
    if (await checkHealth()) return true;
    retryCount.value++;
    loadingMessage.value = `Waiting for server (${retryCount.value}/${MAX_RETRIES})...`;
    await new Promise(r => setTimeout(r, RETRY_DELAY));
  }
  loadingMessage.value = 'Unable to connect to server';
  showSnackbar('Failed to connect. Please refresh.', 'error');
  return false;
}

// ── Maintenance countdown ───────────────────────────────────────────────────

const maintenanceActive = ref(false);
const shutdownAt = ref(null);
const now = ref(Date.now());

let maintenancePollTimer = null;
let countdownTimer = null;
let wasMaintenance = false;

const countdownDisplay = computed(() => {
  if (!shutdownAt.value) return '--';
  const remaining = Math.max(0, Math.ceil((shutdownAt.value - now.value) / 1000));
  const m = Math.floor(remaining / 60);
  const s = remaining % 60;
  return `${m}:${String(s).padStart(2, '0')}`;
});

async function pollMaintenance() {
  const result = await store.checkMaintenance();
  if (result.active) {
    shutdownAt.value = result.shutdownAt;
    maintenanceActive.value = true;
    wasMaintenance = true;
  } else {
    if (wasMaintenance) {
      window.location.reload();
      return;
    }
    maintenanceActive.value = false;
    shutdownAt.value = null;
  }
}

function startMaintenancePolling() {
  pollMaintenance();
  maintenancePollTimer = setInterval(pollMaintenance, MAINTENANCE_POLL_INTERVAL);
  countdownTimer = setInterval(() => { now.value = Date.now(); }, 1000);
}

// ── Lifecycle ─────────────────────────────────────────────────────────────────

onMounted(async () => {
  try {
    const ready = await waitForBackend();
    if (ready) {
      loadingMessage.value = 'Loading data...';
      await store.loadInitialData();
    }
  } catch (error) {
    console.error('[App] Initialization error:', error);
    showSnackbar('An error occurred while loading data.', 'error');
  } finally {
    isLoading.value = false;
    startMaintenancePolling();
  }
});

onUnmounted(() => {
  if (maintenancePollTimer) clearInterval(maintenancePollTimer);
  if (countdownTimer) clearInterval(countdownTimer);
});
</script>

<style scoped>
.maintenance-offset :deep(.v-application__wrap) {
  padding-top: 44px;
}

.loading-overlay {
  background-image: v-bind('`url(${loadingBg})`');
  background-size: cover;
  background-position: center;
}

.loading-overlay::before {
  content: '';
  position: absolute;
  inset: 0;
  background-color: rgba(0, 0, 0, 0.75);
  z-index: 1;
}

.loading-overlay .text-center {
  position: relative;
  z-index: 2;
}

.maintenance-banner {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  z-index: 10000;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 10px 16px;
  height: 44px;
  background-color: #e65100;
  color: #fff;
  font-size: 0.95rem;
  box-shadow: 0 2px 6px rgba(0, 0, 0, 0.25);
}

.v-overlay :deep(.v-overlay__scrim) {
  opacity: 0;
}
</style>