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
  {
    path: '/supervisor',
    component: Supervisor,
    meta: { requireAuth: true },
    children: [
      { path: '', name: 'supervisor', redirect: { name: 'supervisor-prod-report' } },
      { path: 'prod-report', name: 'supervisor-prod-report', component: Supervisor, meta: { requireAuth: true, tab: 0 } },
      { path: 'machine-management', name: 'supervisor-machine-management', component: Supervisor, meta: { requireAuth: true, tab: 1 } },
      { path: 'staff-assignment', name: 'supervisor-staff-assignment', component: Supervisor, meta: { requireAuth: true, tab: 2 } },
      { path: 'product-database', name: 'supervisor-product-database', component: Supervisor, meta: { requireAuth: true, tab: 3 } },
    ],
  },
  {
    path: '/setting',
    component: Setting,
    meta: { requireAuth: true },
    children: [
      { path: '', name: 'setting', redirect: { name: 'setting-password' } },
      { path: 'password', name: 'setting-password', component: Setting, meta: { requireAuth: true, tab: 'password' } },
      { path: 'plc-signals', name: 'setting-plc-signals', component: Setting, meta: { requireAuth: true, tab: 'signals' } },
      { path: 'report', name: 'setting-report', component: Setting, meta: { requireAuth: true, tab: 'report' } },
      { path: 'import-report', name: 'setting-import-report', component: Setting, meta: { requireAuth: true, tab: 'import' } },
      { path: 'machine-names', name: 'setting-machine-names', component: Setting, meta: { requireAuth: true, tab: 'machines' } },
      { path: 'material-groups', name: 'setting-material-groups', component: Setting, meta: { requireAuth: true, tab: 'material' } },
      { path: 'db-log', name: 'setting-db-log', component: Setting, meta: { requireAuth: true, tab: 'dblog' } },
    ],
  },
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