<template>
  <v-container fluid class="pa-4">
    <div class="text-subtitle-1 font-weight-bold mb-4">PLC Department Passwords</div>

    <v-row>
      <v-col v-for="dept in DEPARTMENTS"
             :key="dept.key"
             cols="12" md="4">
        <v-card variant="outlined" class="pa-4 rounded-lg">
          <div class="d-flex align-center mb-3">
            <v-icon :color="dept.color" class="mr-2">{{ dept.icon }}</v-icon>
            <span class="text-subtitle-2 font-weight-bold">{{ dept.label }}</span>
            <v-spacer />
            <v-checkbox v-model="selectedDepts"
                        :value="dept.key"
                        hide-details
                        density="compact" />
          </div>
          <v-text-field v-model.number="passwordForm[dept.key]"
                        label="New Password"
                        type="number"
                        variant="outlined"
                        density="compact"
                        hide-details
                        :disabled="!selectedDepts.includes(dept.key)" />
        </v-card>
      </v-col>
    </v-row>

    <div class="d-flex align-center mt-4 ga-3">
      <v-btn variant="outlined" @click="resetPasswordForm">Reset</v-btn>
      <v-btn color="warning"
             :disabled="selectedDepts.length === 0"
             @click="confirmDialog = true">
        Write to PLC
      </v-btn>
      <span v-if="statusMessage" :class="statusColor" class="text-body-2">
        {{ statusMessage }}
      </span>
    </div>

    <!-- Confirm dialog -->
    <v-dialog v-model="confirmDialog" max-width="400px">
      <v-card>
        <v-card-title class="text-subtitle-1 font-weight-bold py-3 px-4 d-flex align-center ga-2">
          <v-icon color="warning">mdi-alert-outline</v-icon>
          Confirm Password Update
        </v-card-title>
        <v-divider />
        <v-card-text class="pt-4">
          <p class="text-body-2 mb-3">
            The following departments will have their PLC passwords updated:
          </p>
          <v-chip v-for="key in selectedDepts"
                  :key="key"
                  :color="getDept(key)?.color"
                  size="small"
                  class="mr-1 mb-1">
            {{ getDept(key)?.label }} → {{ passwordForm[key] }}
          </v-chip>
          <p class="text-caption text-medium-emphasis mt-3">
            Ensure no active jobs are running before proceeding.
          </p>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-3">
          <v-spacer />
          <v-btn variant="text" @click="confirmDialog = false">Cancel</v-btn>
          <v-btn color="warning" variant="flat" :loading="saving" @click="applyPasswords">
            Confirm & Write to PLC
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </v-container>
</template>

<script setup>
import { ref } from 'vue';
import { DEPARTMENTS } from '@/utils/constant.js';

const defaultPasswords = () =>
  Object.fromEntries(DEPARTMENTS.map(d => [d.key, 0]));

const passwordForm   = ref(defaultPasswords());
const selectedDepts  = ref(DEPARTMENTS.map(d => d.key));
const saving         = ref(false);
const statusMessage  = ref('');
const statusColor    = ref('text-success');
const confirmDialog  = ref(false);

const getDept = key => DEPARTMENTS.find(d => d.key === key);

function resetPasswordForm() {
  passwordForm.value  = defaultPasswords();
  selectedDepts.value = DEPARTMENTS.map(d => d.key);
  statusMessage.value = '';
}

async function applyPasswords() {
  saving.value        = true;
  confirmDialog.value = false;
  statusMessage.value = '';

  const payload = Object.fromEntries(
    selectedDepts.value.map(key => [key, passwordForm.value[key]])
  );

  try {
    const res = await fetch('/api/setting/password', {
      method:  'PUT',
      headers: { 'Content-Type': 'application/json' },
      body:    JSON.stringify(payload),
    });
    if (!res.ok) {
      const err = await res.json().catch(() => ({ error: res.statusText }));
      throw new Error(err.error ?? 'Unknown error');
    }
    statusMessage.value = '✓ Passwords written to PLC successfully.';
    statusColor.value   = 'text-success';
  } catch (err) {
    statusMessage.value = `✗ Failed: ${err.message}`;
    statusColor.value   = 'text-error';
  } finally {
    saving.value = false;
  }
}
</script>
