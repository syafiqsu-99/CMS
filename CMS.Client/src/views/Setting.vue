<template>
  <v-container fluid class="pa-2 h-100 d-flex flex-column">
    <v-row no-gutters align="center" justify="center">
      <v-col cols="12" class="text-center">
        <h2 class="font-weight-bold">SETTINGS</h2>
      </v-col>
    </v-row>

    <v-row no-gutters class="flex-grow-1 flex-shrink-1" style="min-height: 0;">
      <v-col cols="12" class="pa-1 d-flex flex-column h-100">
        <v-card variant="text"
                elevation="2"
                class="d-flex flex-column h-100 overflow-hidden">
          <v-card-text class="d-flex flex-column pa-2 h-100" style="min-height: 0;">

            <v-tabs v-model="activeTab" color="primary" density="compact" class="flex-shrink-0">
              <v-tab value="password">
                <v-icon start>mdi-lock-outline</v-icon>
                Password
              </v-tab>
              <v-tab value="signals">
                <v-icon start>mdi-sine-wave</v-icon>
                PLC Signals
              </v-tab>
              <v-tab value="dblog">
                <v-icon start>mdi-database-eye</v-icon>
                DB Log
              </v-tab>
            </v-tabs>

            <v-window v-model="activeTab" class="flex-grow-1 h-100" style="min-height: 0;">
              <v-window-item value="password" class="h-100 overflow-auto">
                <PasswordTab />
              </v-window-item>
              <v-window-item value="signals" class="h-100 overflow-hidden">
                <PlcSignalMonitor />
              </v-window-item>
              <v-window-item value="dblog" class="h-100 overflow-hidden">
                <DbLogTab />
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
  import DbLogTab from '@/components/Setting/DbLogTab.vue';

  const activeTab = ref('password');
</script>
