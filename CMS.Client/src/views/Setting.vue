<!-- CMS.Client/src/views/Setting.vue -->
<template>
  <v-container fluid class="pa-2 h-100 d-flex flex-column">
    <v-row no-gutters align="center" justify="center">
      <v-col cols="12" class="text-center">
        <h2 class="font-weight-bold">SETTINGS</h2>
      </v-col>
    </v-row>

    <v-row class="flex-grow-1" no-gutters>
      <v-col cols="12" class="pa-1 d-flex" style="height: 100%;">
        <v-card variant="text" elevation="2" style="width: 100%; height: 100%; overflow: hidden;"
                class="d-flex flex-column">
          <v-card-text class="d-flex flex-column pa-2" style="height: 100%; overflow: hidden;">
            <v-tabs v-model="activeTab" color="primary" density="compact" class="flex-grow-0">
              <v-tab value="password">
                <v-icon start>mdi-lock-outline</v-icon>
                Password
              </v-tab>
              <v-tab value="signals">
                <v-icon start>mdi-sine-wave</v-icon>
                PLC Signals
              </v-tab>
            </v-tabs>

            <v-window v-model="activeTab" style="flex: 1; overflow: hidden;">
              <v-window-item value="password" style="height: 100%; overflow-y: auto;">
                <PasswordTab />
              </v-window-item>
              <v-window-item value="signals" style="height: 100%; overflow: hidden;">
                <PlcSignalMonitor />
              </v-window-item>
            </v-window>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>
  </v-container>
</template>

<script setup>
  import { ref } from 'vue';
  import PasswordTab from '@/components/Setting/PasswordTab.vue';
  import PlcSignalMonitor from '@/components/Setting/PlcSignalMonitor.vue';

  const activeTab = ref('password');
</script>
