<template>
  <v-row dense style="flex-direction: column; overflow: hidden; height: 82vh;">

    <!-- Toolbar -->
    <v-col cols="auto">
      <v-toolbar flat density="compact" class="px-2">
        <v-icon color="primary" size="small">mdi-database</v-icon>
        <v-toolbar-title class="text-subtitle-1 ml-2">Product Database</v-toolbar-title>
        <v-spacer />
        <v-text-field v-model="productSearch"
                      label="Search products..."
                      prepend-inner-icon="mdi-magnify"
                      variant="outlined"
                      density="compact"
                      hide-details
                      single-line
                      clearable
                      style="max-width: 300px;" />
      </v-toolbar>
    </v-col>

    <!-- Table -->
    <v-col style="flex: 1; overflow-y: auto; min-height: 0;">
      <v-row dense class="pa-1">
        <v-form ref="editForm" @submit.prevent>
          <v-data-table-virtual :headers="productHeaders"
                                fixed-header
                                :items="filteredProducts"
                                style="height: 65vh;"
                                :loading="loading"
                                loading-text="Loading products..."
                                density="compact"
                                class="elevation-1 fixed-table">

            <template v-slot:item.type="{ item }">
              <input v-if="isEditing(item)" v-model="item.type" type="text" class="inline-input" />
              <span v-else class="text-caption">{{ item.type ?? 'N/A' }}</span>
            </template>

            <template v-slot:item.qty_perct="{ item }">
              <input v-if="isEditing(item)" v-model="item.qty_perct" type="number" class="inline-input" />
              <span v-else class="text-caption">{{ item.qty_perct ?? 'N/A' }}</span>
            </template>

            <template v-slot:item.process="{ item }">
              <select v-if="isEditing(item)" v-model="item.process" class="inline-input rounded">
                <option v-for="p in processOptions" :key="p" :value="p">{{ p }}</option>
              </select>
              <span v-else class="text-caption">{{ item.process ?? 'N/A' }}</span>
            </template>

            <template v-slot:item.material="{ item }">
              <input v-if="isEditing(item)" v-model="item.material" type="text" class="inline-input" />
              <span v-else class="text-caption">{{ item.material ?? 'N/A' }}</span>
            </template>

            <template v-for="col in floatCols" v-slot:[`item.${col}`]="{ item }" :key="col">
              <input v-if="isEditing(item)" v-model="item[col]" type="number" step="0.01" class="inline-input" />
              <span v-else class="text-caption">{{ Number(item[col]).toFixed(2) }}</span>
            </template>

            <template v-slot:item.actions="{ item }">
              <div class="d-flex ga-1">
                <template v-if="isEditing(item)">
                  <v-btn icon="mdi-content-save" color="success" @click="saveEdit(item)" variant="text" size="x-small" />
                  <v-btn icon="mdi-close" color="error" @click="cancelEdit" variant="text" size="x-small" />
                </template>
                <template v-else>
                  <v-btn icon="mdi-pencil" color="success" @click="startEdit(item)" variant="text" size="x-small" />
                  <v-btn icon="mdi-delete" color="error" @click="promptDelete(item)" variant="text" size="x-small" />
                </template>
              </div>
            </template>

          </v-data-table-virtual>
        </v-form>

        <div class="d-flex ga-2 pa-1">
          <v-btn color="primary" @click="promptInsert" size="small">
            <v-icon size="small">mdi-plus</v-icon>Add Product
          </v-btn>
          <v-btn color="success" @click="triggerImport" size="small">
            <v-icon size="small">mdi-upload</v-icon>Import CSV
          </v-btn>
          <v-btn color="warning" @click="exportCSV" size="small">
            <v-icon size="small">mdi-download</v-icon>Export CSV
          </v-btn>
        </div>

        <input ref="fileInput" type="file" accept=".csv" style="display:none" @change="handleImport" />
      </v-row>
    </v-col>
  </v-row>

  <!-- Delete dialog -->
  <v-dialog v-model="deleteDialog.visible" max-width="500px">
    <v-card>
      <v-card-title class="text-h6">
        <v-icon color="warning" class="mr-2">mdi-alert</v-icon>Confirm Deletion
      </v-card-title>
      <v-card-text>
        Are you sure you want to delete this record?<br />
        <strong>SAP:</strong> {{ deleteDialog.item?.id_type }}<br />
        <strong>Mould:</strong> {{ deleteDialog.item?.mould }}
        <p class="text-error mt-3 mb-0">This action cannot be undone.</p>
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn text @click="deleteDialog.visible = false">Cancel</v-btn>
        <v-btn color="error" text @click="confirmDelete">Delete</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>

  <!-- Insert dialog -->
  <v-dialog v-model="insertDialog.visible" max-width="500px">
    <v-card>
      <v-card-title class="py-2">
        <v-icon class="mr-2">mdi-plus-box</v-icon>
        <span class="text-h6">New Product Entry</span>
      </v-card-title>
      <v-divider />
      <v-card-text class="pt-3">
        <v-alert v-if="insertError" type="error" density="compact" class="mb-3">{{ insertError }}</v-alert>
        <v-alert v-if="duplicateWarning" type="warning" density="compact" class="mb-3">{{ duplicateWarning }}</v-alert>
        <v-form ref="insertForm" @submit.prevent>
          <v-row dense>
            <v-col cols="6">
              <v-text-field v-model="insertForm.id_type" label="SAP *" variant="outlined" type="number" density="compact"
                            :rules="[v => !!v || 'Required', v => v >= 0 || 'Must be positive']" />
            </v-col>
            <v-col cols="6">
              <v-number-input v-model="insertForm.mould" label="Mould *" variant="outlined" density="compact" :min="0" :max="10"
                              :rules="[v => v !== null || 'Required']" />
            </v-col>
            <v-col cols="6">
              <v-text-field v-model="insertForm.type" label="Type *" variant="outlined" density="compact"
                            :rules="[v => !!v || 'Required']" />
            </v-col>
            <v-col cols="6">
              <v-text-field v-model="insertForm.qty_perct" label="Cav" variant="outlined" type="number" density="compact" suffix="qty/ct" />
            </v-col>
            <v-col cols="6">
              <v-select v-model="insertForm.process" label="Process" :items="processOptions" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="6">
              <v-text-field v-model="insertForm.material" label="Material" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="6">
              <v-text-field v-model="insertForm.part_weight" label="Part Weight" type="number" variant="outlined" density="compact" suffix="g" />
            </v-col>
            <v-col cols="6">
              <v-text-field v-model="insertForm.tolerance" label="Tolerance" type="number" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="6">
              <v-text-field v-model="insertForm.gross_weight" label="Gross Weight" type="number" variant="outlined" density="compact" suffix="g" />
            </v-col>
            <v-col cols="6">
              <v-text-field v-model="insertForm.sap_ct" label="SAP CT" type="number" variant="outlined" density="compact" suffix="sec" />
            </v-col>
          </v-row>
        </v-form>
      </v-card-text>
      <v-divider />
      <v-card-actions class="py-2">
        <v-spacer />
        <v-btn variant="text" @click="insertDialog.visible = false">Cancel</v-btn>
        <v-btn color="primary" variant="elevated" @click="confirmInsert" :disabled="!!duplicateWarning || !isInsertValid">Save</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>

  <!-- Import preview dialog -->
  <v-dialog v-model="importDialog.visible" max-width="1000px">
    <v-card>
      <v-card-title class="bg-primary">
        <span class="text-h6">Import Preview</span>
      </v-card-title>
      <v-card-text class="pt-4">
        <v-alert v-if="importDialog.errors.length" type="error" density="compact" class="mb-3">
          {{ importDialog.errors.length }} error(s) found
        </v-alert>
        <v-alert v-else type="success" density="compact" class="mb-3">
          Ready to import {{ importDialog.data.length }} row(s)
        </v-alert>
        <div style="max-height: 350px; overflow-y: auto;">
          <v-data-table-virtual :headers="productHeaders.filter(h => h.key !== 'actions')"
                                fixed-header
                                :items="importDialog.data"
                                style="height: 50vh;"
                                density="compact"
                                class="elevation-1" />
        </div>
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn color="grey" variant="text" @click="cancelImport">Cancel</v-btn>
        <v-btn color="primary" variant="elevated" @click="confirmImport"
               :disabled="importDialog.errors.length > 0" :loading="loading">Import</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup>
  import { ref, computed, watch, onMounted, inject } from 'vue';
  import { useMachineStore } from '@/store/machineStore';

  // ── Props / emits ─────────────────────────────────────────────────────────────

  const props = defineProps({
    SAPData: { type: Array, required: true, default: () => [] },
  });
  const emit = defineEmits(['refresh-data']);

  const showSnackbar = inject('showSnackbar');
  const store = useMachineStore();

  // ── State ─────────────────────────────────────────────────────────────────────

  const productSearch = ref('');
  const loading = ref(false);
  const products = ref([]);
  const editItem = ref(null);
  const editKeys = ref({});
  const insertError = ref('');
  const fileInput = ref(null);
  const editFormRef = ref(null);
  const insertFormRef = ref(null);

  const deleteDialog = ref({ visible: false, item: null });
  const insertDialog = ref({ visible: false });
  const importDialog = ref({ visible: false, data: [], errors: [] });

  const emptyInsert = () => ({
    id_type: null, mould: null, type: '', qty_perct: null,
    process: '', material: '', part_weight: null, tolerance: null,
    gross_weight: null, sap_ct: null,
  });
  const insertForm = ref(emptyInsert());

  const processOptions = ['ISBM', 'INJ', 'EBM', 'N/A'];
  const floatCols = ['part_weight', 'tolerance', 'gross_weight', 'sap_ct'];

  const productHeaders = [
    { title: 'SAP', key: 'id_type', width: '8%' },
    { title: 'Mould', key: 'mould', width: '5%' },
    { title: 'Type', key: 'type', width: '30%' },
    { title: 'Cav', key: 'qty_perct', width: '7%' },
    { title: 'Process', key: 'process', width: '8%' },
    { title: 'Material', key: 'material', width: '10%' },
    { title: 'Part Weight', key: 'part_weight', width: '6%' },
    { title: 'Tolerance', key: 'tolerance', width: '6%' },
    { title: 'Gross Weight', key: 'gross_weight', width: '6%' },
    { title: 'SAP CT', key: 'sap_ct', width: '7%' },
    { title: 'Actions', key: 'actions', width: '8%', sortable: false },
  ];

  // ── Computed ──────────────────────────────────────────────────────────────────

  const filteredProducts = computed(() => {
    if (!productSearch.value) return products.value;
    const q = productSearch.value.toLowerCase();
    return products.value.filter(p => Object.values(p).some(v => String(v).toLowerCase().includes(q)));
  });

  const duplicateWarning = computed(() => {
    const { id_type, mould } = insertForm.value;
    if (!id_type || mould === null || mould === undefined) return '';
    const exists = props.SAPData.some(
      p => Number(p.id_type) === Number(id_type) && Number(p.mould) === Number(mould)
    );
    return exists ? `SAP ${id_type} / Mould ${mould} already exists!` : '';
  });

  const isInsertValid = computed(() => {
    const { id_type, mould, type } = insertForm.value;
    return id_type !== null && mould !== null && type?.trim();
  });

  watch(() => props.SAPData, (val) => {
    products.value = [...val];
  }, { immediate: true, deep: false });

  // ── Edit helpers ──────────────────────────────────────────────────────────────

  function isEditing(item) { return editItem.value === item; }

  function startEdit(item) {
    editKeys.value = { id_type: item.id_type, mould: item.mould };
    editItem.value = item;
  }

  async function saveEdit(item) {
    const payload = buildPayload(item, editKeys.value);
    try {
      const res = await fetch('/api/SAP', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      });
      if (!res.ok) throw new Error('Update failed');
      editItem.value = null;
      showSnackbar(`SAP ${item.id_type} updated.`, 'success');
      emit('refresh-data');
    } catch (err) {
      showSnackbar(err.message, 'error');
    }
  }

  function cancelEdit() { editItem.value = null; }

  // ── Insert ────────────────────────────────────────────────────────────────────

  function promptInsert() {
    insertForm.value = emptyInsert();
    insertError.value = '';
    insertDialog.value.visible = true;
  }

  async function confirmInsert() {
    insertError.value = '';
    if (duplicateWarning.value) { insertError.value = duplicateWarning.value; return; }

    const payload = buildPayload(insertForm.value, {});
    try {
      const res = await fetch('/api/SAP', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      });
      if (!res.ok) throw new Error('Insert failed');
      products.value.push({ ...payload });
      insertDialog.value.visible = false;
      emit('refresh-data');
      showSnackbar('Product added.', 'success');
    } catch (err) {
      insertError.value = err.message;
    }
  }

  // ── Delete ────────────────────────────────────────────────────────────────────

  function promptDelete(item) { deleteDialog.value = { visible: true, item }; }

  async function confirmDelete() {
    const { id_type, mould } = deleteDialog.value.item;
    try {
      const res = await fetch(`/api/SAP?id_type=${id_type}&mould=${mould}`, { method: 'DELETE' });
      if (!res.ok) throw new Error('Delete failed');
      // Targeted local removal — no full re-fetch
      products.value = products.value.filter(p => !(p.id_type === id_type && p.mould === mould));
      deleteDialog.value.visible = false;
      emit('refresh-data');
      showSnackbar('Product deleted.', 'success');
    } catch (err) {
      showSnackbar(err.message, 'error');
    }
  }

  // ── Export CSV ────────────────────────────────────────────────────────────────

  function exportCSV() {
    const headers = productHeaders.filter(h => h.key !== 'actions').map(h => h.title);
    const rows = products.value.map(p =>
      productHeaders.filter(h => h.key !== 'actions').map(h => {
        let v = p[h.key] ?? '';
        if (floatCols.includes(h.key) && v !== '') v = Number(v).toFixed(2);
        v = String(v);
        return v.includes(',') || v.includes('"') ? `"${v.replace(/"/g, '""')}"` : v;
      }).join(',')
    );

    const blob = new Blob([[headers.join(','), ...rows].join('\n')], { type: 'text/csv;charset=utf-8;' });
    const a = Object.assign(document.createElement('a'), {
      href: URL.createObjectURL(blob),
      download: `SAP_Products_${new Date().toISOString().split('T')[0]}.csv`,
    });
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    showSnackbar('Export complete.', 'success');
  }

  // ── Import CSV ────────────────────────────────────────────────────────────────

  function triggerImport() { fileInput.value?.click(); }

  function handleImport(event) {
    const file = event.target.files[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = e => parseCSV(e.target.result);
    reader.readAsText(file);
    event.target.value = '';
  }

  function parseCSV(text) {
    const lines = text.split('\n').filter(l => l.trim());
    if (lines.length < 2) { showSnackbar('CSV is empty or invalid', 'error'); return; }

    const headers = parseCSVLine(lines[0]);
    const keyMap = {};
    productHeaders.filter(h => h.key !== 'actions').forEach(h => {
      const idx = headers.findIndex(v => v.toLowerCase().trim() === h.title.toLowerCase().trim());
      if (idx !== -1) keyMap[h.key] = idx;
    });

    const data = [], errors = [];
    for (let i = 1; i < lines.length; i++) {
      const vals = parseCSVLine(lines[i].trim());
      const row = Object.fromEntries(
        Object.entries(keyMap).map(([k, idx]) => [k, vals[idx] ?? null])
      );
      if (!row.id_type) errors.push(`Row ${i + 1}: SAP required`);
      if (!row.mould) errors.push(`Row ${i + 1}: Mould required`);
      if (!row.type) errors.push(`Row ${i + 1}: Type required`);
      ['id_type', 'mould', 'qty_perct', 'part_weight', 'tolerance', 'gross_weight', 'sap_ct'].forEach(k => {
        if (row[k] !== null && row[k] !== '') {
          const n = Number(row[k]); row[k] = isNaN(n) ? null : n;
        }
      });
      data.push(row);
    }

    importDialog.value = { visible: true, data, errors };
  }

  function parseCSVLine(line) {
    const result = []; let cur = '', inQ = false;
    for (let i = 0; i < line.length; i++) {
      const c = line[i], n = line[i + 1];
      if (c === '"') { if (inQ && n === '"') { cur += '"'; i++; } else inQ = !inQ; }
      else if (c === ',' && !inQ) { result.push(cur.trim()); cur = ''; }
      else cur += c;
    }
    result.push(cur.trim());
    return result;
  }

  async function confirmImport() {
    loading.value = true;
    try {
      const res = await fetch('/api/SAP/ImportSAP', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(importDialog.value.data.map(buildPayload)),
      });
      if (!res.ok) throw new Error('Import failed');
      products.value = [...importDialog.value.data];
      importDialog.value.visible = false;
      emit('refresh-data');
      showSnackbar(`${importDialog.value.data.length} products imported.`, 'success');
    } catch (err) {
      showSnackbar(err.message, 'error');
    } finally {
      loading.value = false;
    }
  }

  function cancelImport() { importDialog.value = { visible: false, data: [], errors: [] }; }

  // ── Shared payload builder ────────────────────────────────────────────────────

  function buildPayload(item, keys = {}) {
    return {
      id_type: Number(item.id_type) || 0,
      mould: Number(item.mould) || 0,
      type: item.type || 'N/A',
      qty_perct: Number(item.qty_perct) || 0,
      process: item.process || 'N/A',
      material: item.material || 'N/A',
      part_weight: Number(item.part_weight) || 0,
      tolerance: Number(item.tolerance) || 0,
      gross_weight: Number(item.gross_weight) || 0,
      sap_ct: Number(item.sap_ct) || 0,
      ...(keys.id_type !== undefined ? { keys_id_type: Number(keys.id_type), keys_mould: Number(keys.mould) } : {}),
    };
  }
</script>

<style scoped>
  .inline-input {
    width: 100%;
    border: 1px solid #e0e0e0;
    background: white;
    padding: 4px 8px;
    box-sizing: border-box;
    font-size: 0.75rem;
  }

  .fixed-table :deep(table) {
    table-layout: fixed !important;
  }

  .fixed-table :deep(td) {
    padding: 1px !important;
    height: 40px;
  }

  .fixed-table :deep(td:has(span)) {
    padding: 0 12px !important;
  }
</style>
