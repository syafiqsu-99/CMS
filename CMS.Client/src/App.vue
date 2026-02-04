<template>
  <v-app>
    <v-overlay v-model="isLoading"
               persistent
               class="align-center justify-center loading-overlay"
               style="z-index: 9999">
      <div class="text-center">
        <v-progress-circular indeterminate
                             size="80"
                             width="8"
                             color="white"></v-progress-circular>
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

  <v-snackbar v-model="snackbar.show" :color="snackbar.color" :timeout="3000">
    {{ snackbar.message }}
    <template #actions>
      <v-btn variant="text" @click="snackbar.show = false">Close</v-btn>
    </template>
  </v-snackbar>
</template>

<script setup>
  import { ref, onMounted, watch, provide, onBeforeUnmount } from "vue";
  import { useRouter, useRoute } from "vue-router";
  import { pinia } from "@/store/index";
  import NavBar from './components/NavBar.vue'

  const router = useRouter();
  const route = useRoute();
  const store = pinia();

  const isLoading = ref(true);
  const loadingMessage = ref("Connecting to server...");
  const retryCount = ref(0);
  const maxRetries = 10;
  const retryDelay = 2000;

  const snackbar = ref({
    show: false,
    message: "",
    color: "success",
  });

  function showSnackbar(message, color = "success") {
    snackbar.value.message = message;
    snackbar.value.color = color;
    snackbar.value.show = true;
  }

  provide("showSnackbar", showSnackbar);

  function getShift() {
    const now = new Date();
    const currentHour = now.getHours();
    return (currentHour >= 6 && currentHour < 18) ? 1 : 2;
  }

  function getDate() {
    const today = new Date();
    return today.toISOString().split('T')[0];
  }

  function formatDate(date) {
    if (date instanceof Date) {
      return date.toLocaleDateString('en-CA');
    }
    if (typeof date === 'string') {
      if (/^\d{4}-\d{2}-\d{2}$/.test(date)) {
        return date;
      }
      const parsedDate = new Date(date);
      return parsedDate.toLocaleDateString('en-CA');
    }
    return null;
  }

  async function checkBackendHealth() {
    try {
      const response = await fetch('/api/MachineLog/Health', {
        method: 'GET',
        headers: {
          'Content-Type': 'application/json',
        },
      });

      if (response.ok) {
        const data = await response.json();
        return data.status === 'Ready';
      }
      return false;
    } catch (error) {
      console.error('Health check failed:', error);
      return false;
    }
  }

  async function waitForBackend() {
    while (retryCount.value < maxRetries) {
      const isHealthy = await checkBackendHealth();

      if (isHealthy) {
        loadingMessage.value = "Loading data...";
        return true;
      }

      retryCount.value++;
      loadingMessage.value = `Waiting for server (${retryCount.value}/${maxRetries})...`;

      await new Promise(resolve => setTimeout(resolve, retryDelay));
    }

    loadingMessage.value = "Unable to connect to server";
    showSnackbar("Failed to connect to server. Please refresh the page.", "error");
    return false;
  }

  async function loadInitialData() {
    try {
      await Promise.all([
        store.loadDailyReport(formatDate(getDate()), getShift()),
        store.loadAttendance(),
        store.loadSAP(),
        store.loadMachineMaster()
      ]);
    } catch (error) {
      console.error('Error loading initial data:', error);
      showSnackbar("Error loading initial data", "error");
    }
  }

  let machineInterval = null;

  onMounted(async () => {
    const backendReady = await waitForBackend();

    if (backendReady) {
      await loadInitialData();

      machineInterval = setInterval(() => {
        store.loadMachineMaster();
      }, 10000);

      isLoading.value = false;
    } else {
      isLoading.value = false;
    }
  });

  onBeforeUnmount(() => {
    if (machineInterval) clearInterval(machineInterval);
  });
</script>x

<style scoped>
  .loading-overlay {
    background-image: url('../dist/jjbackground.png');
    background-size: cover;
    background-position: center;
    background-repeat: no-repeat;
  }

  .loading-overlay::before {
    content: '';
    position: absolute;
    top: 0;
    left: 0;
    right: 0;
    bottom: 0;
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
