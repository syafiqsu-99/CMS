import { createRouter, createWebHistory } from "vue-router";
import Dashboard from '../views/Dashboard.vue'
import Machines from '../views/Machines.vue'
import Setting from '../views/Setting.vue'
import OEE from '../views/OEE.vue'
import Supervisor from '../views/Supervisor.vue'

const routes = [
  { path: '/', redirect: { name: 'dashboard' } },
  { path: '/dashboard', name: 'dashboard', component: Dashboard, meta: { title: 'Dashboard' } },
  { path: '/machines', name: 'machines', component: Machines, meta: { title: 'Machines' } },
  { path: '/oee', name: 'oee', component: OEE, meta: { title: 'OEE' } },
  {
    path: '/supervisor',
    component: Supervisor,
    meta: { requireAuth: true },
    children: [
      { path: '', name: 'supervisor', redirect: { name: 'supervisor-prod-report' } },
      { path: 'prod-report', name: 'supervisor-prod-report', component: Supervisor, meta: { requireAuth: true, tab: 0, parent: 'Supervisor', title: 'Production Report' } },
      { path: 'machine-management', name: 'supervisor-machine-management', component: Supervisor, meta: { requireAuth: true, tab: 1, parent: 'Supervisor', title: 'Machine Management' } },
      { path: 'staff-assignment', name: 'supervisor-staff-assignment', component: Supervisor, meta: { requireAuth: true, tab: 2, parent: 'Supervisor', title: 'Staff Assignment' } },
      { path: 'product-database', name: 'supervisor-product-database', component: Supervisor, meta: { requireAuth: true, tab: 3, parent: 'Supervisor', title: 'Product Database' } },
    ],
  },
  {
    path: '/setting',
    component: Setting,
    meta: { requireAuth: true },
    children: [
      { path: '', name: 'setting', redirect: { name: 'setting-password' } },
      { path: 'password', name: 'setting-password', component: Setting, meta: { requireAuth: true, tab: 'password', parent: 'Setting', title: 'Password' } },
      { path: 'plc-signals', name: 'setting-plc-signals', component: Setting, meta: { requireAuth: true, tab: 'signals', parent: 'Setting', title: 'PLC Signals' } },
      { path: 'report', name: 'setting-report', component: Setting, meta: { requireAuth: true, tab: 'report', parent: 'Setting', title: 'Report' } },
      { path: 'import-report', name: 'setting-import-report', component: Setting, meta: { requireAuth: true, tab: 'import', parent: 'Setting', title: 'Import Report' } },
      { path: 'machine-names', name: 'setting-machine-names', component: Setting, meta: { requireAuth: true, tab: 'machines', parent: 'Setting', title: 'Machine Names' } },
      { path: 'material-groups', name: 'setting-material-groups', component: Setting, meta: { requireAuth: true, tab: 'material', parent: 'Setting', title: 'Material Groups' } },
      { path: 'network', name: 'setting-network', component: Setting, meta: { requireAuth: true, tab: 'network', parent: 'Setting', title: 'Network & Schema' } },
      { path: 'db-log', name: 'setting-db-log', component: Setting, meta: { requireAuth: true, tab: 'dblog', parent: 'Setting', title: 'DB Log' } },
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