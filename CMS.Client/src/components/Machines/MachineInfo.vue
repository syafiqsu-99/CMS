<template>
  <!-- Empty state -->
  <div v-if="!machine"
       class="d-flex flex-column align-center justify-center"
       style="height: 100%;">
    <v-icon size="80" color="grey-lighten-2">mdi-monitor-dashboard</v-icon>
    <div class="text-h6 text-grey-darken-1 mt-4 font-weight-medium">
      Select a machine to view details
    </div>
    <div class="text-caption text-grey mt-2">Choose from the list on the left</div>
  </div>

  <!-- Detail panel -->
  <div v-else>
    <!-- Header -->
    <div class="mb-4 d-flex align-center justify-space-between">
      <div>
        <v-card-title class="text-h5 pa-0 mb-1">
          <strong>{{ machine.machine_name }}</strong>
        </v-card-title>
        <v-card-subtitle class="text-subtitle-2 pa-0 text-grey-darken-1">
          {{ machine.type }}
        </v-card-subtitle>
      </div>
      <v-chip :color="machine.color" size="large" class="font-weight-bold">
        {{ machine.category || 'No Data' }}
      </v-chip>
    </div>

    <!-- Machine Information card -->
    <v-card variant="flat" class="pa-4 mb-4 rounded-lg" elevation="1">
      <div class="d-flex align-center mb-3">
        <v-icon size="20" color="primary" class="mr-2">mdi-information-outline</v-icon>
        <span class="text-subtitle-2 font-weight-bold">Machine Information</span>
      </div>
      <slot name="info" />
    </v-card>

    <!-- Timeline Gantt chart -->
    <v-card variant="flat" class="pa-3 mb-4 rounded-lg" elevation="1">
      <div class="d-flex align-center mb-3">
        <v-icon size="20" color="primary" class="mr-2">mdi-chart-timeline</v-icon>
        <span class="text-subtitle-2 font-weight-bold">Today's Timeline</span>
      </div>
      <slot name="timeline" />
    </v-card>

    <!-- Utility timeline -->
    <v-card variant="flat" class="pa-3 rounded-lg" elevation="1">
      <div class="d-flex align-center mb-3">
        <v-icon size="20" color="primary" class="mr-2">mdi-flash-outline</v-icon>
        <span class="text-subtitle-2 font-weight-bold">Utility Timeline</span>
      </div>
      <slot name="utilities" />
    </v-card>
  </div>
</template>

<script setup>
defineProps({
  machine: { type: Object, default: null },
});
</script>
