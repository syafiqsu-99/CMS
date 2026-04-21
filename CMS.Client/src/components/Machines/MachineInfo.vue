<template>
  <v-card variant="flat" class="pa-2 bg-grey-lighten-5" style="height: 100%; overflow-y: auto;" elevation="0">
    <div v-if="machine && machineData">
      <div class="mb-4 d-flex align-center justify-space-between">
        <div>
          <v-card-title class="text-h5 pa-0 mb-1">
            <strong>{{ machine.machine_name }}</strong>
          </v-card-title>
          <v-card-subtitle class="text-subtitle-2 pa-0 text-grey-darken-1">
            {{ machineData.type }}
          </v-card-subtitle>
        </div>
        <v-chip :color="machine.currentStatus?.color"
                size="large"
                class="font-weight-bold">
          {{ machine.currentStatus?.category || 'No Data' }}
        </v-chip>
      </div>

      <!-- Machine Information -->
      <v-card variant="flat" class="pa-4 mb-4 rounded-lg" elevation="1">
        <div class="d-flex align-center mb-3">
          <v-icon size="20" color="primary" class="mr-2">mdi-information-outline</v-icon>
          <span class="text-subtitle-2 font-weight-bold">Machine Information</span>
        </div>
        <v-row dense>
          <v-col cols="6" sm="4" md="3">
            <div class="text-caption text-grey-darken-1 mb-1">Model</div>
            <div class="text-body-2 font-weight-medium">{{ machineData.type }}</div>
          </v-col>
          <v-col cols="6" sm="4" md="3">
            <div class="text-caption text-grey-darken-1 mb-1">Packer</div>
            <div class="text-body-2 font-weight-medium">{{ machineData.packer || 'N/A' }}</div>
          </v-col>
          <v-col cols="6" sm="4" md="3">
            <div class="text-caption text-grey-darken-1 mb-1">SAP</div>
            <div class="text-body-2 font-weight-medium">{{ machineData.id_type }}</div>
          </v-col>
          <v-col cols="6" sm="4" md="3">
            <div class="text-caption text-grey-darken-1 mb-1">Mould</div>
            <div class="text-body-2 font-weight-medium">{{ machineData.mould }}</div>
          </v-col>
          <v-col cols="6" sm="4" md="3">
            <div class="text-caption text-grey-darken-1 mb-1">Qty per CT</div>
            <div class="text-body-2 font-weight-medium">{{ machineData.qty_perct }}</div>
          </v-col>
          <v-col cols="6" sm="4" md="3">
            <div class="text-caption text-grey-darken-1 mb-1">Part Weight</div>
            <div class="text-body-2 font-weight-medium">{{ machineData.part_weight }} g</div>
          </v-col>
          <v-col cols="6" sm="4" md="3">
            <div class="text-caption text-grey-darken-1 mb-1">Material</div>
            <div class="text-body-2 font-weight-medium">{{ machineData.material }}</div>
          </v-col>
          <v-col cols="6" sm="4" md="3">
            <div class="text-caption text-grey-darken-1 mb-1">Visual QC</div>
            <div class="text-body-2 font-weight-medium">{{ machineData.visual_qc }}</div>
          </v-col>
          <v-col cols="6" sm="4" md="3">
            <div class="text-caption text-grey-darken-1 mb-1">Measure QC</div>
            <div class="text-body-2 font-weight-medium">{{ machineData.measure_qc }}</div>
          </v-col>
        </v-row>
      </v-card>

      <!-- Metrics Cards -->
      <v-row dense>
        <v-col cols="12" sm="6" md="3">
          <v-card variant="flat" color="primary" class="pa-3 rounded-lg white--text" elevation="2" style="height: 130px;">
            <div class="d-flex flex-column justify-space-between" style="height: 100%;">
              <div>
                <div class="text-overline opacity-90">Output</div>
                <div class="text-h4 font-weight-bold mt-1">
                  {{ machineData.output || 0 }}
                  <span class="text-body-2">pcs</span>
                </div>
              </div>
              <div class="text-caption opacity-80">
                Plan: {{ machineData.planned_output || 0 }} pcs
              </div>
            </div>
          </v-card>
        </v-col>

        <v-col cols="12" sm="6" md="3">
          <v-card variant="flat" color="success" class="pa-3 rounded-lg white--text" elevation="2" style="height: 130px;">
            <div class="d-flex flex-column justify-space-between" style="height: 100%;">
              <div>
                <div class="text-overline opacity-90">Efficiency</div>
                <div class="text-h4 font-weight-bold mt-1">
                  {{ calculateEfficiency(machineData) }}%
                </div>
              </div>
              <v-progress-linear :model-value="parseFloat(calculateEfficiency(machineData))"
                                 color="white"
                                 bg-color="rgba(255,255,255,0.3)"
                                 height="6"
                                 rounded
                                 class="mt-2"></v-progress-linear>
            </div>
          </v-card>
        </v-col>

        <v-col cols="12" sm="6" md="3">
          <v-card variant="flat" color="info" class="pa-3 rounded-lg white--text" elevation="2" style="height: 130px;">
            <div class="d-flex flex-column justify-space-between" style="height: 100%;">
              <div>
                <div class="text-overline opacity-90">Cycle Time</div>
                <div class="text-h4 font-weight-bold mt-1">
                  {{ (machineData.act_ct || 0).toFixed(2) }}
                  <span class="text-body-2">sec</span>
                </div>
              </div>
              <div class="text-caption opacity-80">
                SAP CT: {{ (machineData.sap_ct || 0).toFixed(2) }} sec
              </div>
            </div>
          </v-card>
        </v-col>

        <v-col cols="12" sm="6" md="3">
          <v-card variant="flat" color="error" class="pa-3 rounded-lg white--text" elevation="2" style="height: 130px;">
            <div class="d-flex flex-column justify-space-between" style="height: 100%;">
              <div>
                <div class="text-overline opacity-90">Reject</div>
                <div class="text-h4 font-weight-bold mt-1">
                  {{ machineData.reject_pcs || 0 }}
                  <span class="text-body-2">pcs</span>
                </div>
              </div>
              <div>
                <div class="text-caption opacity-80">Weight: {{ (machineData.reject_weight || 0).toFixed(2) }} kg</div>
                <div class="text-caption opacity-80">Rate: {{ calculateRejectRate(machineData) }}%</div>
              </div>
            </div>
          </v-card>
        </v-col>

        <!-- Remark/Problem Alert -->
        <v-col cols="12" v-if="machineData.problem">
          <v-card variant="flat" class="pa-3 rounded-lg" color="amber-lighten-5" elevation="1">
            <div class="d-flex align-start">
              <v-icon size="20" color="warning" class="mr-2 mt-1">mdi-alert-circle-outline</v-icon>
              <div>
                <div class="text-subtitle-2 font-weight-bold mb-1 text-warning">Remark</div>
                <div class="text-body-2">{{ machineData.problem }}</div>
              </div>
            </div>
          </v-card>
        </v-col>

        <!-- Production Timeline -->
        <v-col cols="12">
          <v-card variant="flat" class="pa-3 rounded-lg" elevation="1">
            <div class="d-flex align-center mb-3">
              <v-icon size="20" color="primary" class="mr-2">mdi-chart-timeline-variant</v-icon>
              <span class="text-subtitle-2 font-weight-bold">Production Timeline</span>
            </div>
            <slot name="timeline"></slot>
          </v-card>
        </v-col>

        <!--Utility Data-->
        <v-col cols="12">
          <v-card variant="flat" class="pa-3 rounded-lg" elevation="1">
            <div class="d-flex align-center mb-3">
              <v-icon size="20" color="primary" class="mr-2">mdi-flash-outline</v-icon>
              <span class="text-subtitle-2 font-weight-bold">Utility Timeline</span>
            </div>
            <slot name="utilities"></slot>
          </v-card>
        </v-col>
      </v-row>
    </div>

    <div v-else class="d-flex flex-column align-center justify-center" style="height: 100%;">
      <v-icon size="80" color="grey-lighten-2">mdi-monitor-dashboard</v-icon>
      <div class="text-h6 text-grey-darken-1 mt-4 font-weight-medium">Select a machine to view details</div>
      <div class="text-caption text-grey mt-2">Choose from the list on the left</div>
    </div>
  </v-card>
</template>

<script setup>
  defineProps({
    machine: { type: Object, default: null },
    machineData: { type: Object, default: null },
  });

  const calculateEfficiency = (m) => {
    if (!m || !m.planned_output || m.planned_output === 0) return 0;
    const result = ((m.output || 0) / m.planned_output) * 100;
    return isNaN(result) ? "0.0" : result.toFixed(1);
  };

  const calculateRejectRate = (m) => {
    if (!m) return "0.0";
    const totalProduction = (m.output || 0) + (m.reject_pcs || 0);
    if (totalProduction === 0) return "0.0";

    const result = ((m.reject_pcs || 0) / totalProduction) * 100;
    return isNaN(result) ? "0.0" : result.toFixed(1);
  };
</script>
