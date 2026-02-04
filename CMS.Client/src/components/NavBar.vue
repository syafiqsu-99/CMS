<template>
  <v-layout class="h-screen">
    <v-navigation-drawer expand-on-hover permanent rail class="nav-bar" @update:rail="isRail = $event">
      <v-list>
        <v-list-item prepend-avatar="/JJlogo.png" subtitle="CMS" title="Central Monitoring System"></v-list-item>
      </v-list>

      <v-divider></v-divider>

      <v-list>
        <v-list-item prepend-icon="mdi-monitor-dashboard" title="Dashboard" :to="{ name: 'dashboard' }" link></v-list-item>

        <v-list-item prepend-icon="mdi-view-comfy" title="Machines" :to="{ name: 'machines' }" link></v-list-item>

        <v-list-item prepend-icon="mdi-database" title="OEE" :to="{ name: 'oee' }" link></v-list-item>

        <v-list-item v-if="loggedIn" prepend-icon="mdi-account" title="Supervisor" :to="{ name: 'supervisor' }" link></v-list-item>

        <v-list-item v-if="loggedIn" prepend-icon="mdi-cog" title="Setting" :to="{ name: 'setting' }" link></v-list-item>
      </v-list>

      <template #append>
        <v-divider />

        <v-list>
          <v-list-item prepend-icon="mdi-login"
                       title="Login"
                       v-if="!loggedIn"
                       @click="dialog = true" />

          <v-list-item prepend-icon="mdi-logout"
                       title="Logout"
                       v-else
                       @click="logout" />
        </v-list>
      </template>
    </v-navigation-drawer>

    <v-main class="d-flex flex-column" style="height: 100vh;">
      <router-view />
    </v-main>

    <v-dialog v-model="dialog" width="400px">
      <v-card class="pa-4">
        <h3 class="mb-4">Enter Password</h3>

        <v-text-field v-model="password"
                      variant="outlined"
                      label="Password"
                      type="password"
                      @keyup.enter="login" />

        <v-btn block color="primary" class="mt-3" @click="login">
          Login
        </v-btn>
      </v-card>
    </v-dialog>
  </v-layout>
</template>

<script setup>
  import { ref, inject } from 'vue';
  import { useRouter } from 'vue-router';

  const router = useRouter();

  const showSnackbar = inject("showSnackbar");
  const dialog = ref(false);
  const password = ref('');
  const loggedIn = ref(localStorage.getItem('logged_in') === 'true');

  const login = () => {
    if (password.value === "jjpmsb1234") {
      localStorage.setItem('logged_in', 'true');
      loggedIn.value = true;
      dialog.value = false;
      password.value = '';
    } else {
      showSnackbar(`Invalid password!`, "error");
    }
  };

  const logout = () => {
    localStorage.removeItem('logged_in');
    loggedIn.value = false;

    if (router.currentRoute.value.meta.requireAuth) {
      router.push({ name: "dashboard" });
    }
  };
</script>
