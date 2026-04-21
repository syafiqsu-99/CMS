<template>
  <v-app>
    <!-- Loading overlay -->
    <v-overlay v-model="isLoading"
               persistent
               class="align-center justify-center loading-overlay"
               style="z-index: 9999">
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
  import { ref, provide, onMounted } from 'vue';
  import NavBar from '@/components/NavBar.vue';
  import { useMachineStore } from '@/store/machineStore';

  const store = useMachineStore();

  const isLoading = ref(true);
  const loadingMessage = ref('Connecting to server…');
  const retryCount = ref(0);
  const MAX_RETRIES = 10;
  const RETRY_DELAY = 2000;

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
      loadingMessage.value = `Waiting for server (${retryCount.value}/${MAX_RETRIES})…`;
      await new Promise(r => setTimeout(r, RETRY_DELAY));
    }
    loadingMessage.value = 'Unable to connect to server';
    showSnackbar('Failed to connect. Please refresh.', 'error');
    return false;
  }

  // ── Lifecycle ─────────────────────────────────────────────────────────────────

  onMounted(async () => {
    try {
      const ready = await waitForBackend();
      if (ready) {
        loadingMessage.value = 'Loading data…';
        await store.loadInitialData();
      }
    } catch (error) {
      console.error('[App] Initialization error:', error);
      showSnackbar('An error occurred while loading data.', 'error');
    } finally {
      isLoading.value = false;
    }
  });
</script>

<style scoped>
  .loading-overlay {
    background-image: url('../dist/jjbackground.png');
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

  .v-overlay :deep(.v-overlay__scrim) {
    opacity: 0;
  }
</style>
