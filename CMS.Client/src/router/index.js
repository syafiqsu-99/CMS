import { createRouter, createWebHistory } from "vue-router";
import Dashboard from '../views/Dashboard.vue'
import Machines from '../views/Machines.vue'
import Setting from '../views/Setting.vue'
import OEE from '../views/OEE.vue'
import Supervisor from '../views/Supervisor.vue'

const routes = [
  { path: '/', redirect: { name: 'dashboard' } },
  { path: '/dashboard', name: 'dashboard', component: Dashboard },
  { path: '/machines', name: 'machines', component: Machines },
  { path: '/oee', name: 'oee', component: OEE },
  { path: '/supervisor', name: 'supervisor', component: Supervisor, meta: { requireAuth: true } },
  { path: '/setting', name: 'setting', component: Setting, meta: { requireAuth: true } },
]

const router = createRouter({
  history: createWebHistory(),
  routes,
});

router.beforeEach((to, from, next) => {
  const loggedIn = localStorage.getItem('logged_in') === 'true';

  if (to.meta.requireAuth && !loggedIn) {
    return next({ name: 'dashboard' });
  }

  next();
});

export default router;
