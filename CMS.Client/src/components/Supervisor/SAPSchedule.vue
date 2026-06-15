<template>
  <v-row dense class="d-flex flex-column overflow-hidden" style="height: 82vh;">
    <v-col cols="auto">
      <v-toolbar flat density="compact" class="px-2">
        <v-icon color="primary" size="small">mdi-database</v-icon>
        <v-toolbar-title class="text-subtitle-1 ml-2">Product Database</v-toolbar-title>
        <v-spacer />
        <v-text-field v-model="productSearch"
                      label="Search..."
                      prepend-inner-icon="mdi-magnify"
                      variant="outlined"
                      density="compact"
                      hide-details
                      single-line
                      clearable
                      max-width="300"
                      @update:model-value="onSearchInput" />
        <v-btn class="ml-2" size="small" prepend-icon="mdi-upload" color="success" @click="triggerImport">Import</v-btn>
        <v-btn class="ml-1" size="small" prepend-icon="mdi-download" color="warning" @click="exportToCSV">Export</v-btn>
        <span class="ml-3 text-caption text-medium-emphasis">{{ totalCount.toLocaleString() }} records</span>
      </v-toolbar>
    </v-col>

    <v-col class="flex-grow-1 overflow-hidden" style="min-height: 0;">
      <v-data-table-virtual :headers="productHeaders"
                            :items="products"
                            :loading="loading && products.length === 0"
                            density="compact"
                            fixed-header
                            class="elevation-1 h-100">

        <template #item.type="{ item }">
          <input v-if="isEditing(item)" v-model="item.type" type="text" class="w-100 border rounded bg-white px-2 py-1 text-caption" />
          <span v-else class="text-caption">{{ item.type ?? 'N/A' }}</span>
        </template>

        <template #item.qty_perct="{ item }">
          <input v-if="isEditing(item)" v-model="item.qty_perct" type="number" class="w-100 border rounded bg-white px-2 py-1 text-caption" />
          <span v-else class="text-caption">{{ item.qty_perct ?? 'N/A' }}</span>
        </template>

        <template #item.process="{ item }">
          <select v-if="isEditing(item)" v-model="item.process" class="w-100 border rounded bg-white px-2 py-1 text-caption">
            <option v-for="p in processOptions" :key="p" :value="p">{{ p }}</option>
          </select>
          <span v-else class="text-caption">{{ item.process ?? 'N/A' }}</span>
        </template>

        <template #item.material="{ item }">
          <input v-if="isEditing(item)" v-model="item.material" type="text" class="w-100 border rounded bg-white px-2 py-1 text-caption" />
          <span v-else class="text-caption">{{ item.material ?? 'N/A' }}</span>
        </template>

        <template #item.part_weight="{ item }">
          <input v-if="isEditing(item)" v-model="item.part_weight" type="number" step="0.01" class="w-100 border rounded bg-white px-2 py-1 text-caption" />
          <span v-else class="text-caption">{{ Number(item.part_weight).toFixed(2) }}</span>
        </template>

        <template #item.tolerance="{ item }">
          <input v-if="isEditing(item)" v-model="item.tolerance" type="number" step="0.01" class="w-100 border rounded bg-white px-2 py-1 text-caption" />
          <span v-else class="text-caption">{{ Number(item.tolerance).toFixed(2) }}</span>
        </template>

        <template #item.gross_weight="{ item }">
          <input v-if="isEditing(item)" v-model="item.gross_weight" type="number" step="0.01" class="w-100 border rounded bg-white px-2 py-1 text-caption" />
          <span v-else class="text-caption">{{ Number(item.gross_weight).toFixed(2) }}</span>
        </template>

        <template #item.sap_ct="{ item }">
          <input v-if="isEditing(item)" v-model="item.sap_ct" type="number" step="0.01" class="w-100 border rounded bg-white px-2 py-1 text-caption" />
          <span v-else class="text-caption">{{ Number(item.sap_ct).toFixed(2) }}</span>
        </template>

        <template #item.actions="{ item }">
          <div class="d-flex ga-1">
            <template v-if="isEditing(item)">
              <v-btn icon="mdi-content-save" color="success" variant="text" size="x-small" @click="saveEdit(item)" />
              <v-btn icon="mdi-close" color="error" variant="text" size="x-small" @click="cancelEdit" />
            </template>
            <template v-else>
              <v-btn icon="mdi-pencil" color="success" variant="text" size="x-small" @click="startEdit(item)" />
              <v-btn icon="mdi-delete" color="error" variant="text" size="x-small" @click="promptDelete(item)" />
            </template>
          </div>
        </template>

        <template #body.append>
          <tr v-if="!initialLoaded || products.length < totalCount">
            <td colspan="11" class="text-center pa-2">
              <div v-intersect="onIntersect">
                <v-progress-circular v-if="loading" indeterminate color="primary" size="24"></v-progress-circular>
              </div>
            </td>
          </tr>
        </template>
      </v-data-table-virtual>
    </v-col>

    <input ref="fileInput" type="file" accept=".csv" class="d-none" @change="handleFileImport" />
  </v-row>

  <!-- Delete confirm dialog -->
  <v-dialog v-model="deleteDialog.visible" max-width="380">
    <v-card>
      <v-card-title class="bg-error text-white pa-3">Confirm Delete</v-card-title>
      <v-card-text class="pt-4">
        Delete SAP <strong>{{ deleteDialog.item?.id_type }}</strong> / Mould <strong>{{ deleteDialog.item?.mould }}</strong>?
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" @click="deleteDialog.visible = false">Cancel</v-btn>
        <v-btn color="error" variant="elevated" :loading="loading" @click="confirmDelete">Delete</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>

  <!-- Import preview dialog -->
  <v-dialog v-model="importDialog.visible" max-width="1000">
    <v-card>
      <v-card-title class="bg-primary text-white">Import Preview</v-card-title>
      <v-card-text class="pt-4">
        <v-alert type="warning" density="compact" class="mb-3">
          This will <strong>delete all existing SAP records</strong> and replace them with the {{ importDialog.data.length }} row(s) below.
        </v-alert>
        <v-alert v-if="importDialog.errors.length" type="error" density="compact" class="mb-3">{{ importDialog.errors.length }} error(s) found</v-alert>
        <div class="overflow-y-auto" style="max-height: 350px;">
          <v-data-table-virtual :headers="productHeaders.filter(h => h.key !== 'actions')" fixed-header :items="importDialog.data" style="height: 50vh;" density="compact" class="elevation-1" />
        </div>
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn color="grey" variant="text" @click="importDialog.visible = false">Cancel</v-btn>
        <v-btn color="primary" variant="elevated" :disabled="importDialog.errors.length > 0" :loading="loading" @click="confirmImport">Replace All & Import</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup>
  import { ref, inject } from 'vue';

  const emit = defineEmits(['refresh-data']);
  const showSnackbar = inject('showSnackbar');

  const productSearch = ref('');
  const loading = ref(false);
  const products = ref([]);
  const totalCount = ref(0);
  const page = ref(1);
  const pageSize = ref(50);
  const initialLoaded = ref(false);

  const editItem = ref(null);
  const editKeys = ref({});
  const fileInput = ref(null);

  const deleteDialog = ref({ visible: false, item: null });
  const importDialog = ref({ visible: false, data: [], errors: [] });

  const processOptions = ['ISBM', 'INJ', 'EBM', 'N/A'];

  const productHeaders = [
    { title: 'SAP', key: 'id_type', width: '8%' },
    { title: 'Mould', key: 'mould', width: '5%' },
    { title: 'Type', key: 'type', width: '28%' },
    { title: 'Cav', key: 'qty_perct', width: '6%' },
    { title: 'Process', key: 'process', width: '8%' },
    { title: 'Material', key: 'material', width: '10%' },
    { title: 'Part Wt', key: 'part_weight', width: '6%' },
    { title: 'Tol', key: 'tolerance', width: '6%' },
    { title: 'Gross Wt', key: 'gross_weight', width: '6%' },
    { title: 'SAP CT', key: 'sap_ct', width: '7%' },
    { title: 'Actions', key: 'actions', width: '8%', sortable: false }
  ];

  async function loadMore() {
    if (loading.value || (initialLoaded.value && products.value.length >= totalCount.value && totalCount.value > 0)) return;

    loading.value = true;
    try {
      const params = new URLSearchParams({ page: String(page.value), pageSize: String(pageSize.value) });
      if (productSearch.value?.trim()) params.set('search', productSearch.value.trim());

      const res = await fetch(`/api/supervisor/sap/paged?${params}`);
      if (!res.ok) throw new Error(`HTTP ${res.status}`);

      const { items, totalCount: total } = await res.json();
      products.value.push(...items);
      totalCount.value = total;
      page.value++;
      initialLoaded.value = true;
    } catch (err) {
      showSnackbar('Failed to load products.', 'error');
    } finally {
      loading.value = false;
    }
  }

  function onIntersect(isIntersecting) {
    if (isIntersecting) loadMore();
  }

  async function resetAndReload() {
    page.value = 1;
    products.value = [];
    totalCount.value = 0;
    initialLoaded.value = false;
  }

  let searchDebounce = null;
  function onSearchInput() {
    clearTimeout(searchDebounce);
    searchDebounce = setTimeout(() => resetAndReload(), 350);
  }

  function isEditing(item) { return editItem.value === item; }
  function startEdit(item) { editKeys.value = { id_type: item.id_type, mould: item.mould }; editItem.value = item; }
  function cancelEdit() { editItem.value = null; }

  async function saveEdit(item) {
    const payload = buildPayload(item, editKeys.value);
    try {
      const res = await fetch('/api/supervisor/sap', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });
      if (!res.ok) throw new Error('Update failed');
      editItem.value = null;
      showSnackbar(`SAP ${item.id_type} updated.`, 'success');
      emit('refresh-data');
    } catch (err) {
      showSnackbar(err.message, 'error');
    }
  }

  function promptDelete(item) { deleteDialog.value = { visible: true, item }; }

  async function confirmDelete() {
    const { id_type, mould } = deleteDialog.value.item;
    try {
      const res = await fetch(`/api/supervisor/sap?id_type=${id_type}&mould=${mould}`, { method: 'DELETE' });
      if (!res.ok) throw new Error('Delete failed');
      deleteDialog.value.visible = false;
      showSnackbar('Product deleted.', 'success');
      emit('refresh-data');
      await resetAndReload();
    } catch (err) {
      showSnackbar(err.message, 'error');
    }
  }

  function triggerImport() { fileInput.value?.click(); }

  function handleFileImport(e) {
    const file = e.target.files?.[0];
    if (!file) return;

    const reader = new FileReader();
    reader.onload = (ev) => {
      const lines = ev.target.result.split('\n').filter(l => l.trim());
      const header = lines[0].split(',').map(h => h.trim().replace(/"/g, ''));
      const errors = [];
      const data = [];

      for (let i = 1; i < lines.length; i++) {
        const vals = lines[i].split(',').map(v => v.trim().replace(/"/g, ''));
        const row = Object.fromEntries(header.map((h, idx) => [h, vals[idx] ?? '']));
        if (!row.id_type || !row.mould)
          errors.push(`Row ${i + 1}: missing id_type or mould`);
        else
          data.push(row);
      }
      importDialog.value = { visible: true, data, errors };
    };
    reader.readAsText(file);
    e.target.value = '';
  }

  async function confirmImport() {
    loading.value = true;
    try {
      const payload = importDialog.value.data.map(r => ({
        id_type: Number(r.id_type) || 0, mould: Number(r.mould) || 0,
        type: r.type || 'N/A', qty_perct: Number(r.qty_perct) || 0,
        process: r.process || 'N/A', material: r.material || 'N/A',
        part_weight: Number(r.part_weight) || 0, tolerance: Number(r.tolerance) || 0,
        gross_weight: Number(r.gross_weight) || 0, sap_ct: Number(r.sap_ct) || 0
      }));

      const res = await fetch('/api/supervisor/sap/import', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });
      if (!res.ok) throw new Error('Import failed');

      importDialog.value.visible = false;
      showSnackbar(`Replaced all SAP records with ${payload.length} imported rows.`, 'success');
      emit('refresh-data');
      await resetAndReload();
    } catch (err) {
      showSnackbar(err.message, 'error');
    } finally {
      loading.value = false;
    }
  }

  async function exportToCSV() {
    try {
      const res = await fetch('/api/supervisor/sap');
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const data = await res.json();

      const headers = ['id_type', 'mould', 'type', 'qty_perct', 'process', 'material', 'part_weight', 'tolerance', 'gross_weight', 'sap_ct'];
      const rows = data.map(r => headers.map(h => r[h] ?? '').join(','));
      const csv = [headers.join(','), ...rows].join('\n');

      const blob = new Blob([csv], { type: 'text/csv' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `sap_export_${new Date().toISOString().slice(0, 10)}.csv`;
      a.click();
      URL.revokeObjectURL(url);
    } catch (err) {
      showSnackbar('Export failed: ' + err.message, 'error');
    }
  }

  function buildPayload(item, keys) {
    return {
      id_type: Number(item.id_type) || 0, mould: Number(item.mould) || 0,
      type: item.type || 'N/A', qty_perct: Number(item.qty_perct) || 0,
      process: item.process || 'N/A', material: item.material || 'N/A',
      part_weight: Number(item.part_weight) || 0, tolerance: Number(item.tolerance) || 0,
      gross_weight: Number(item.gross_weight) || 0, sap_ct: Number(item.sap_ct) || 0,
      keys_id_type: Number(keys.id_type) || 0, keys_mould: Number(keys.mould) || 0
    };
  }
</script>
