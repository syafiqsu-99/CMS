<template>
  <v-row dense style="flex-direction: column; overflow: hidden; height: 82vh;">
    <v-col cols="auto">
      <v-toolbar flat density="compact" class="px-2">
        <v-icon color="primary" size="small">mdi-database</v-icon>
        <v-toolbar-title class="text-subtitle-1 ml-2">
          Product Database
        </v-toolbar-title>
        <v-spacer></v-spacer>
        <v-text-field v-model="productSearch"
                      label="Search products..."
                      prepend-inner-icon="mdi-magnify"
                      variant="outlined"
                      density="compact"
                      hide-details
                      single-line
                      clearable
                      style="max-width: 300px;"></v-text-field>
      </v-toolbar>
    </v-col>

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
              <input v-if="editMode(item)"
                     v-model="item.type"
                     type="text"
                     class="text-caption pa-1"
                     style="width: 100%; height: 100%; border: 1px solid #e0e0e0; background-color: white; padding: 4px 8px; box-sizing: border-box;" />
              <span v-else class="text-caption">{{ item.type ?? 'N/A' }}</span>
            </template>

            <template v-slot:item.qty_perct="{ item }">
              <input v-if="editMode(item)"
                     v-model="item.qty_perct"
                     type="number"
                     class="text-caption pa-1"
                     style="width: 100%; height: 100%; border: 1px solid #e0e0e0; background-color: white; padding: 4px 8px; box-sizing: border-box;" />
              <span v-else class="text-caption">{{ item.qty_perct ?? 'N/A' }}</span>
            </template>

            <template v-slot:item.process="{ item }">
              <select v-if="editMode(item)"
                      v-model="item.process"
                      class="text-caption pa-1 rounded"
                      style="width: 100%; height: 100%; border: 1px solid #e0e0e0; background-color: white; cursor: pointer; padding: 4px 8px; box-sizing: border-box; ">
                <option v-for="process in processSelect" :key="process" :value="process">{{ process }}</option>
              </select>
              <span v-else class="text-caption">{{ item.process ?? 'N/A' }}</span>
            </template>

            <template v-slot:item.material="{ item }">
              <input v-if="editMode(item)"
                     v-model="item.material"
                     type="text"
                     class="text-caption pa-1"
                     style="width: 100%; height: 100%; border: 1px solid #e0e0e0; background-color: white; padding: 4px 8px; box-sizing: border-box;" />
              <span v-else class="text-caption">{{ item.material ?? 'N/A' }}</span>
            </template>

            <template v-slot:item.part_weight="{ item }">
              <input v-if="editMode(item)"
                     v-model="item.part_weight"
                     type="number"
                     step="0.01"
                     class="text-caption pa-1"
                     style="width: 100%; height: 100%; border: 1px solid #e0e0e0; background-color: white; padding: 4px 8px; box-sizing: border-box;" />
              <span v-else class="text-caption">{{ Number(item.part_weight).toFixed(2) ?? 0.00 }}</span>
            </template>

            <template v-slot:item.tolerance="{ item }">
              <input v-if="editMode(item)"
                     v-model="item.tolerance"
                     type="number"
                     step="0.01"
                     class="text-caption pa-1"
                     style="width: 100%; height: 100%; border: 1px solid #e0e0e0; background-color: white; padding: 4px 8px; box-sizing: border-box;" />
              <span v-else class="text-caption">{{ Number(item.tolerance).toFixed(2) ?? 0.00 }}</span>
            </template>

            <template v-slot:item.gross_weight="{ item }">
              <input v-if="editMode(item)"
                     v-model="item.gross_weight"
                     type="number"
                     step="0.01"
                     class="text-caption pa-1"
                     style="width: 100%; height: 100%; border: 1px solid #e0e0e0; background-color: white; padding: 4px 8px; box-sizing: border-box;" />
              <span v-else class="text-caption">{{ Number(item.gross_weight).toFixed(2) ?? 0.00 }}</span>
            </template>

            <template v-slot:item.sap_ct="{ item }">
              <input v-if="editMode(item)"
                     v-model="item.sap_ct"
                     type="number"
                     step="0.01"
                     class="text-caption pa-1"
                     style="width: 100%; height: 100%; border: 1px solid #e0e0e0; background-color: white; padding: 4px 8px; box-sizing: border-box;" />
              <span v-else class="text-caption">{{ Number(item.sap_ct).toFixed(2) ?? 0.00 }}</span>
            </template>

            <template v-slot:item.actions="{ item }">
              <div class="d-flex ga-1">
                <template v-if="editMode(item)">
                  <v-btn icon="mdi-content-save" color="success" @click="saveChanges(item)" variant="text" size="x-small" />
                  <v-btn icon="mdi-close" color="error" @click="cancelEdit" variant="text" size="x-small" />
                </template>
                <template v-else>
                  <v-btn icon="mdi-pencil" color="success" @click="editProduct(item)" variant="text" size="x-small" />
                  <v-btn icon="mdi-delete" color="error" @click="promptDelete(item)" variant="text" size="x-small" />
                </template>
              </div>
            </template>
          </v-data-table-virtual>
        </v-form>

        <div class="d-flex ga-2 pa-1">
          <v-btn color="primary" @click="promptInsert" size="small">
            <v-icon size="small">mdi-plus</v-icon>
            Add Product
          </v-btn>
          <v-btn color="success" @click="triggerImport" size="small">
            <v-icon size="small">mdi-upload</v-icon>
            Import CSV
          </v-btn>
          <v-btn color="warning" @click="exportToCSV" size="small">
            <v-icon size="small">mdi-download</v-icon>
            Export CSV
          </v-btn>
        </div>
        <input ref="fileInput"
               type="file"
               accept=".csv"
               style="display: none;"
               @change="handleFileImport" />
      </v-row>
    </v-col>
  </v-row>

  <v-dialog v-model="importDialog.visible" max-width="1000px">
    <v-card>
      <v-card-title class="bg-primary">
        <span class="text-h6">Import Preview</span>
      </v-card-title>
      <v-card-text class="pt-4">
        <v-alert v-if="importDialog.errors.length > 0" type="error" density="compact" class="mb-3">
          <div class="text-body-2 font-weight-bold">{{ importDialog.errors.length }} error(s) found:</div>
          <ul class="mt-2">
            <li v-for="(error, idx) in importDialog.errors.slice(0, 5)" :key="idx" class="text-caption">
              {{ error }}
            </li>
          </ul>
          <div v-if="importDialog.errors.length > 5" class="text-caption mt-1">
            ...and {{ importDialog.errors.length - 5 }} more errors
          </div>
        </v-alert>

        <v-alert v-else type="success" density="compact" class="mb-3">
          Ready to import {{ importDialog.data.length }} row(s)
        </v-alert>

        <div style="max-height: 350px; overflow-y: auto;">
          <v-data-table-virtual :headers="productHeaders.filter(h => h.key !== 'actions')"
                                fixed-header
                                :items="importDialog.data"
                                style="height: 50vh;"
                                loading-text="Loading products..."
                                density="compact"
                                class="elevation-1 fixed-table"/>
        </div>
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn color="grey" variant="text" @click="cancelImport">Cancel</v-btn>
        <v-btn color="primary"
               variant="elevated"
               @click="confirmImport"
               :disabled="importDialog.errors.length > 0">
          Import
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>

  <v-dialog v-model="deleteDialog.visible" max-width="500px">
    <v-card>
      <v-card-title class="text-h6">
        <v-icon color="warning" class="mr-2">mdi-alert</v-icon>
        Confirm Deletion
      </v-card-title>

      <v-card-text>
        Are you sure you want to delete this record?<br />
        <strong>SAP:</strong> {{ deleteDialog.item?.id_type }}<br />
        <strong>Mould:</strong> {{ deleteDialog.item?.mould }}<br />
        <p class="text-danger mt-3 mb-0">This action cannot be undone.</p>
      </v-card-text>

      <v-card-actions>
        <v-spacer />
        <v-btn text @click="deleteDialog.visible = false">Cancel</v-btn>
        <v-btn color="error" text @click="confirmDelete">Delete</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>

  <v-dialog v-model="insertDialog.visible" max-width="500px">
    <v-card>
      <v-card-title class="py-2">
        <v-icon class="mr-2">mdi-plus-box</v-icon>
        <span class="text-h6">New Product Entry</span>
      </v-card-title>

      <v-divider></v-divider>

      <v-card-text class="pt-3">
        <v-alert v-if="insertError"
                 type="error"
                 density="compact"
                 class="mb-3">
          {{ insertError }}
        </v-alert>

        <v-alert v-if="existingCheck.exists"
                 type="warning"
                 density="compact"
                 class="mb-3">
          {{ existingCheck.message }}
        </v-alert>

        <v-form ref="insertForm" @submit.prevent>
          <v-row dense>
            <v-col cols="6">
              <v-text-field v-model="insertDialog.item.id_type"
                            label="SAP *"
                            variant="outlined"
                            type="number"
                            density="compact"
                            :rules="[
                            v=>
                !!v || 'SAP required',
                v => (v >= 0) || 'Must be positive',
                v => (v <= 999999) || 'Max 6 digits']"/>
            </v-col>

            <v-col cols="6">
              <v-number-input v-model="insertDialog.item.mould"
                              label="Mould *"
                              variant="outlined"
                              density="compact"
                              :min="0"
                              :max="10"
                              :rules="[v => v !== '' && v !== null || 'Required']" />
            </v-col>

            <v-col cols="6">
              <v-text-field v-model="insertDialog.item.type"
                            label="Type *"
                            variant="outlined"
                            density="compact"
                            :rules="[v => !!v || 'Required']" />
            </v-col>

            <v-col cols="6">
              <v-text-field v-model="insertDialog.item.qty_perct"
                            label="Cav"
                            variant="outlined"
                            type="number"
                            density="compact"
                            suffix="qty/ct" />
            </v-col>

            <v-col cols="6">
              <v-select v-model="insertDialog.item.process"
                        label="Process"
                        :items="processSelect"
                        variant="outlined"
                        density="compact" />
            </v-col>

            <v-col cols="6">
              <v-text-field v-model="insertDialog.item.material"
                            label="Material"
                            variant="outlined"
                            density="compact" />
            </v-col>

            <v-col cols="6">
              <v-text-field v-model="insertDialog.item.part_weight"
                            label="Part Weight"
                            type="number"
                            variant="outlined"
                            density="compact"
                            suffix="g" />
            </v-col>

            <v-col cols="6">
              <v-text-field v-model="insertDialog.item.tolerance"
                            label="Tolerance"
                            type="number"
                            variant="outlined"
                            density="compact" />
            </v-col>

            <v-col cols="6">
              <v-text-field v-model="insertDialog.item.gross_weight"
                            label="Gross Weight"
                            type="number"
                            variant="outlined"
                            density="compact"
                            suffix="g" />
            </v-col>

            <v-col cols="6">
              <v-text-field v-model="insertDialog.item.sap_ct"
                            label="SAP CT"
                            type="number"
                            variant="outlined"
                            density="compact"
                            suffix="sec" />
            </v-col>
          </v-row>
        </v-form>
      </v-card-text>

      <v-divider></v-divider>

      <v-card-actions class="py-2">
        <v-spacer />
        <v-btn variant="text" @click="insertDialog.visible = false">
          Cancel
        </v-btn>
        <v-btn color="primary"
               variant="elevated"
               @click="confirmInsert"
               :disabled="existingCheck.exists || !isFormValid">
          Save
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup>
  import { ref, computed, inject, onMounted, watch } from 'vue';

  const productSearch = ref('');
  const loading = ref(false);
  const products = ref([]);
  const keys = ref({});
  const editItem = ref(null);
  const insertError = ref('');
  const editForm = ref(null);
  const insertForm = ref(null);
  const fileInput = ref(null);
  const showSnackbar = inject("showSnackbar");
  const deleteDialog = ref({
    visible: false,
    item: null
  });
  const insertDialog = ref({
    visible: false,
    item: {
      id_type: null,
      mould: null,
      type: '',
      qty_perct: null,
      process: '',
      material: '',
      part_weight: null,
      tolerance: null,
      gross_weight: null,
      sap_ct: null
    }
  });
  const importDialog = ref({
    visible: false,
    data: [],
    errors: []
  });
  const emit = defineEmits(['refresh-data']);

  const { SAPData } = defineProps({
    SAPData: {
      type: Array,
      required: true,
      default: () => []
    }
  });

  const existingCheck = ref({
    exists: false,
    message: ""
  });

  const productHeaders = [
    { title: 'SAP'          , key: 'id_type'      , width: '8%' },
    { title: 'Mould'        , key: 'mould'        , width: '5%' },
    { title: 'Type'         , key: 'type'         , width: '30%' },
    { title: 'Cav'          , key: 'qty_perct'    , width: '7%' },
    { title: 'Process'      , key: 'process'      , width: '8%' },
    { title: 'Material'     , key: 'material'     , width: '10%' },
    { title: 'Part Weight'  , key: 'part_weight'  , width: '6%' },
    { title: 'Tolerance'    , key: 'tolerance'    , width: '6%' },
    { title: 'Gross Weight' , key: 'gross_weight' , width: '6%' },
    { title: 'SAP CT'       , key: 'sap_ct'       , width: '7%' },
    { title: 'Actions'      , key: 'actions'      , width: '8%', sortable: false }
  ];

  const processSelect = ['ISBM', 'INJ', 'EBM', 'N/A'];

  const filteredProducts = computed(() => {
    if (!productSearch.value) return products.value;
    const search = productSearch.value.toLowerCase();
    return products.value.filter(product =>
      Object.values(product).some(v =>
        String(v).toLowerCase().includes(search)
      )
    );
  });

  const isFormValid = computed(() => {
    const item = insertDialog.value.item;

    const hasIdType = item.id_type !== null && item.id_type !== undefined && item.id_type !== '';
    const hasMould = item.mould !== null && item.mould !== undefined && item.mould !== '';
    const hasType = item.type && item.type.trim() !== '';

    const isIdTypeValid = hasIdType && Number(item.id_type) >= 0 && Number(item.id_type) <= 999999;

    const isMouldValid = hasMould && Number(item.mould) >= 0 && Number(item.mould) <= 10;

    return hasIdType && isIdTypeValid && hasMould && isMouldValid && hasType;
  });

  watch(
    () => [insertDialog.value.item.id_type, insertDialog.value.item.mould],
    ([id_type, mould]) => {
      existingCheck.value.exists = false;
      existingCheck.value.message = "";

      if (!id_type || mould === null || mould === undefined || mould === '') return;

      const exists = SAPData.some(
        p =>
          Number(p.id_type) === Number(id_type) &&
          Number(p.mould) === Number(mould)
      );

      if (exists) {
        existingCheck.value.exists = true;
        existingCheck.value.message = `SAP ${id_type} with Mould ${mould} already exists!`;
      }
    }
  );

  async function loadProducts() {
    loading.value = true;
    products.value = SAPData;
    loading.value = false;
  }

  function promptInsert() {
    insertDialog.value.item = {
      id_type: null,
      mould: null,
      type: '',
      qty_perct: null,
      process: '',
      material: '',
      part_weight: null,
      tolerance: null,
      gross_weight: null,
      sap_ct: null
    };
    insertDialog.value.visible = true;
  }

  async function confirmInsert() {
    insertError.value = '';

    if (existingCheck.value.exists) {
      insertError.value = existingCheck.value.message;
      return;
    }

    const { valid } = await insertForm.value.validate();
    if (!valid) {
      insertError.value = 'Please fix any errors before submitting.';
      return;
    }

    const item = insertDialog.value.item;

    const payload = {
      id_type: Number(item.id_type) || 0,
      mould: Number(item.mould) || 0,
      type: item.type || 'N/A',
      qty_perct: Number(item.qty_perct) || 0,
      process: item.process || 'N/A',
      material: item.material || 'N/A',
      part_weight: Number(item.part_weight) || 0,
      tolerance: Number(item.tolerance) || 0,
      gross_weight: Number(item.gross_weight) || 0,
      sap_ct: Number(item.sap_ct) || 0
    };

    try {
      const response = await fetch('/api/MachineLog/SAP', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });

      if (response.ok) {
        emit('refresh-data');

        await store.loadSAPData();

        insertDialog.value.visible = false;

        insertDialog.value.item = {
          id_type: null,
          mould: null,
          type: '',
          qty_perct: null,
          process: null,
          material: '',
          part_weight: null,
          tolerance: null,
          gross_weight: null,
          sap_ct: null
        };
      } else {
        insertError.value = 'Insert failed.';
      }
    } catch (error) {
      console.error('Network error:', error);
      insertError.value = 'Network error occurred.';
    }
  }

  function editMode(item) {
    return editItem.value && item === editItem.value;
  }

  function editProduct(item) {
    keys.value = {
      id_type: item.id_type,
      mould: item.mould
    }
    editItem.value = item;
  }

  async function saveChanges(item) {
    if (!editForm.value) return;

    const { valid } = await editForm.value.validate();
    if (!valid) {
      showSnackbar(`Make sure SAP, Mould and Type are not empty before saving.`, "error");
      return;
    }

    const payload = {
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
      keys_id_type: Number(keys.value.id_type),
      keys_mould: Number(keys.value.mould)
    };

    try {
      const response = await fetch('/api/MachineLog/SAP', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });

      if (response.ok) {
        const index = products.value.findIndex(
          p => Number(p.id_type) === Number(keys.value.id_type) &&
            Number(p.mould) === Number(keys.value.mould)
        );

        if (index !== -1) {
          products.value[index] = { ...payload };
        }

        editItem.value = null;
        showSnackbar(`Product ${keys.value.id_type} updated successfully!`, "success");
      } else {
        const errorData = await response.json().catch(() => ({}));
        showSnackbar(`Failed to update product ${keys.value.id_type}. Please try again.`, "error");
      }
    } catch (error) {
      console.error('Error updating SAP:', error);
      showSnackbar(`Network error occurred. Please check your connection.`, "error");
    }

    editItem.value = null;
  }

  function cancelEdit() {
    editItem.value = null;
  }

  function promptDelete(item) {
    deleteDialog.value.item = item;
    deleteDialog.value.visible = true;
  }

  async function confirmDelete() {
    const { id_type, mould } = deleteDialog.value.item;

    try {
      const res = await fetch(`/api/MachineLog/SAP?id_type=${id_type}&mould=${mould}`, {
        method: 'DELETE'
      });

      if (!res.ok) {
        throw new Error('Failed to delete record');
      }

      products.value = products.value.filter(
        p => !(p.id_type === id_type && p.mould === mould)
      );
    } catch (err) {
      showSnackbar(err.message, "error");
    } finally {
      deleteDialog.value.visible = false;
    }
  }

  function exportToCSV() {
    try {
      const headers = productHeaders
        .filter(h => h.key !== 'actions')
        .map(h => h.title);

      const csvRows = [];
      csvRows.push(headers.join(','));

      products.value.forEach(product => {
        const row = productHeaders
          .filter(h => h.key !== 'actions')
          .map(h => {
            let value = product[h.key];

            if (value === null || value === undefined) {
              value = '';
            }

            if (h.key === 'part_weight' || h.key === 'tolerance' ||
              h.key === 'gross_weight' || h.key === 'sap_ct') {
              value = value !== '' ? Number(value).toFixed(2) : '';
            }

            value = String(value);
            if (value.includes(',') || value.includes('"') || value.includes('\n')) {
              value = `"${value.replace(/"/g, '""')}"`;
            }

            return value;
          });
        csvRows.push(row.join(','));
      });

      const csvContent = csvRows.join('\n');
      const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
      const link = document.createElement('a');
      const url = URL.createObjectURL(blob);

      link.setAttribute('href', url);
      link.setAttribute('download', `SAP_Products_${new Date().toISOString().split('T')[0]}.csv`);
      link.style.visibility = 'hidden';
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);

      showSnackbar("Products exported successfully", "success");
    } catch (error) {
      showSnackbar(`Error exporting products: ${error.message}`, "error");
    }
  }

  function triggerImport() {
    fileInput.value.click();
  }

  function handleFileImport(event) {
    const file = event.target.files[0];
    if (!file) return;

    const reader = new FileReader();
    reader.onload = (e) => {
      try {
        const csvText = e.target.result;
        parseCSV(csvText);
      } catch (error) {
        showSnackbar(`Error reading file: ${error.message}`, "error");
      }
    };
    reader.readAsText(file);

    event.target.value = '';
  }

  function parseCSV(csvText) {
    const lines = csvText.split('\n').filter(line => line.trim());
    if (lines.length < 2) {
      showSnackbar("CSV file is empty or invalid", "error");
      return;
    }

    const headerLine = lines[0];
    const headers = parseCSVLine(headerLine);

    const headerMap = {};
    productHeaders.filter(h => h.key !== 'actions').forEach(h => {
      const index = headers.findIndex(csvHeader =>
        csvHeader.toLowerCase().trim() === h.title.toLowerCase().trim()
      );
      if (index !== -1) {
        headerMap[h.key] = index;
      }
    });

    const importedData = [];
    const errors = [];

    for (let i = 1; i < lines.length; i++) {
      const line = lines[i].trim();
      if (!line) continue;

      const values = parseCSVLine(line);
      const rowNum = i + 1;

      const row = {
        id_type: values[headerMap['id_type']] || null,
        mould: values[headerMap['mould']] || null,
        type: values[headerMap['type']] || '',
        qty_perct: values[headerMap['qty_perct']] || null,
        process: values[headerMap['process']] || '',
        material: values[headerMap['material']] || '',
        part_weight: values[headerMap['part_weight']] || null,
        tolerance: values[headerMap['tolerance']] || null,
        gross_weight: values[headerMap['gross_weight']] || null,
        sap_ct: values[headerMap['sap_ct']] || null
      };

      if (!row.id_type || row.id_type.trim() === '') {
        errors.push(`Row ${rowNum}: SAP is required`);
      }
      if (!row.mould || row.mould.trim() === '') {
        errors.push(`Row ${rowNum}: Mould is required`);
      }
      if (!row.type || row.type.trim() === '') {
        errors.push(`Row ${rowNum}: Type is required`);
      }

      ['id_type', 'mould', 'qty_perct', 'part_weight', 'tolerance', 'gross_weight', 'sap_ct'].forEach(key => {
        if (row[key] !== null && row[key] !== '') {
          const num = Number(row[key]);
          row[key] = isNaN(num) ? null : num;
        }
      });

      importedData.push(row);
    }

    importedData.sort((a, b) => {
      const sapA = Number(a.id_type) || 0;
      const sapB = Number(b.id_type) || 0;

      if (sapA !== sapB) {
        return sapA - sapB;
      }

      const mouldA = Number(a.mould) || 0;
      const mouldB = Number(b.mould) || 0;
      return mouldA - mouldB;
    });

    importDialog.value.data = importedData;
    importDialog.value.errors = errors;
    importDialog.value.visible = true;
  }

  function parseCSVLine(line) {
    const result = [];
    let current = '';
    let inQuotes = false;

    for (let i = 0; i < line.length; i++) {
      const char = line[i];
      const nextChar = line[i + 1];

      if (char === '"') {
        if (inQuotes && nextChar === '"') {
          current += '"';
          i++;
        } else {
          inQuotes = !inQuotes;
        }
      } else if (char === ',' && !inQuotes) {
        result.push(current.trim());
        current = '';
      } else {
        current += char;
      }
    }
    result.push(current.trim());

    return result;
  }

  async function confirmImport() {
    try {
      loading.value = true;

      const importData = importDialog.value.data.map(item => ({
        id_type: item.id_type ?? 0,
        mould: item.mould ?? 0,
        type: item.type || '',
        qty_perct: item.qty_perct ?? 0,
        process: item.process || '',
        material: item.material || '',
        part_weight: item.part_weight ?? 0.0,
        tolerance: item.tolerance ?? 0.0,
        gross_weight: item.gross_weight ?? 0.0,
        sap_ct: item.sap_ct ?? 0.0
      }));

      const response = await fetch('/api/MachineLog/SAP/ImportSAP', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(importData)
      });

      const responseText = await response.text();

      if (!response.ok) {
        let errorMessage = 'Failed to import data';
        try {
          const errorData = JSON.parse(responseText);
          errorMessage = errorData.error || errorMessage;
        } catch {
          errorMessage = responseText || errorMessage;
        }
        throw new Error(errorMessage);
      }

      let result = { message: `Successfully imported ${importData.length} products` };
      if (responseText) {
        try {
          result = JSON.parse(responseText);
        } catch (e) {
          console.warn('Response is not JSON:', responseText);
        }
      }

      products.value = [...importDialog.value.data];

      showSnackbar(result.message || `Successfully imported ${importDialog.value.data.length} products`, "success");

      importDialog.value.visible = false;
      importDialog.value.data = [];
      importDialog.value.errors = [];

      emit('refresh-data');

    } catch (error) {
      showSnackbar(`Error importing products: ${error.message}`, "error");
    } finally {
      loading.value = false;
    }
  }

  function cancelImport() {
    importDialog.value.visible = false;
    importDialog.value.data = [];
    importDialog.value.errors = [];
  }

  onMounted(() => {
    loadProducts();
  });
</script>

<style scoped>
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
