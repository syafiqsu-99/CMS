<template>
  <v-container fluid class="pa-2 h-100 d-flex flex-column" style="overflow-y:auto;">
    <v-row no-gutters align="center" justify="center">
      <v-col cols="12" class="text-center">
        <h2 class="font-weight-bold">SETTING</h2>
      </v-col>
    </v-row>

    <v-row no-gutters class="flex-grow-1 flex-shrink-1" style="height: 90vh;">
      <v-col cols="12" class="pa-1 d-flex" style="height: 100%;">
        <v-card variant="text" class="d-flex flex-column" elevation="2" style="width: 100%; height: 100%; overflow: hidden;">
          <v-card-title class="text-h6 font-weight-bold">Staff Management</v-card-title>

          <v-card-text>
            <v-btn color="primary" class="mb-3" @click="openAddDialog">Add Staff</v-btn>
            <v-data-table-virtual :headers="headers"
                                  :items="staffList"
                                  fixed-header
                                  item-key="staff_id"
                                  dense
                                  style="height: 70vh;"
                                  class="elevation-1">
              <template #item.actions="{ item }">
                <v-btn icon="mdi-pencil" size="small" @click="openEditDialog(item)"></v-btn>
                <v-btn icon="mdi-delete" size="small" color="error" @click="deleteStaff(item)"></v-btn>
              </template>


              <template #item.photo="{ item }">
                <v-img :src="getStaffPhoto(item.staff_id)"
                       width="40"
                       height="55"
                       style="object-fit:cover;border:1px solid #ccc;"></v-img>
              </template>
            </v-data-table-virtual>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

  <v-dialog v-model="dialog" max-width="500px">
    <v-card>
      <v-card-title class="text-h6 font-weight-bold">{{ dialogMode }} Staff</v-card-title>
      <v-card-text>
        <v-text-field v-model="form.staff_name" label="Name" dense></v-text-field>
        <v-text-field v-model="form.staff_role" label="Role" dense></v-text-field>

        <v-file-input v-model="imageFile"
                      label="Staff Photo"
                      accept="image/*"
                      dense
                      prepend-icon="mdi-image"></v-file-input>
      </v-card-text>
      <v-card-actions>
        <v-spacer></v-spacer>
        <v-btn text @click="dialog=false">Cancel</v-btn>
        <v-btn color="primary" @click="saveStaff">Save</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
  </v-container>
</template>

<script setup>
  import { ref, onMounted } from "vue";

  const staffList = ref([]);
  const dialog = ref(false);
  const dialogMode = ref("Add");
  const imageFile = ref(null);

  const form = ref({
    staff_id: null,
    staff_name: "",
    staff_role: "",
  });

  const headers = [
    { title: "Staff ID",  key: "staff_id",    width: '20%' },
    { title: "Photo",     key: "photo",       width: '20%' },
    { title: "Name",      key: "staff_name",  width: '20%' },
    { title: "Role",      key: "staff_role",  width: '20%' },
    { title: "Actions",   key: "actions",     width: '20%',   sortable: false },
  ];

  onMounted(() => loadStaff());

  async function loadStaff() {
    const response = await fetch('/api/MachineLog/StaffSchedule');
    staffList.value = await response.json();
  }

  function openAddDialog() {
    dialogMode.value = "Add";
    form.value = { staff_id: null, staff_name: "", staff_role: "" };
    imageFile.value = null;
    dialog.value = true;
  }

  function openEditDialog(item) {
    dialogMode.value = "Edit";
    form.value = { ...item };
    imageFile.value = null;
    dialog.value = true;
  }

  async function saveStaff() {
    let staffId = form.value.staff_id;

    if (dialogMode.value === "Add") {
      const response = await fetch('/api/MachineLog/Staff', {
        method: 'POST',
        headers: { "Content-Type": 'application/json' },
        body: JSON.stringify(form.value),
      });

      const data = await response.json();
      staffId = data.staff_id;
    }

    else {
      await fetch('/api/MachineLog/Staff', {
        method: 'PUT',
        headers: { "Content-Type": 'application/json' },
        body: JSON.stringify(form.value),
      });
    }

    if (imageFile.value) {
      const fd = new FormData();
      fd.append("file", imageFile.value);
      fd.append("staff_id", staffId);

      await fetch("/api/MachineLog/StaffPhoto", {
        method: 'POST',
        body: fd,
      });
    }

    dialog.value = false;
    await loadStaff();
  }

  async function deleteStaff(item) {
    await fetch('/api/MachineLog/Staff', {
      method: 'DELETE',
      headers: { "Content-Type": 'application/json' },
      body: JSON.stringify({ staff_id: item.staff_id }),
    });

    await loadStaff();
  }

  function getStaffPhoto(staff_id) {
    const photoUrl = `/api/MachineLog/StaffPhoto/${staff_id}?t=${Date.now()}`;
    return photoUrl;
  }
</script>
