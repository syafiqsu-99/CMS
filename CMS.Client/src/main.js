import './assets/main.css';

import { createApp } from 'vue';
import { createPinia } from 'pinia';
import router from './router';
import App from './App.vue';

import 'vuetify/styles';
import { createVuetify } from 'vuetify';
import { aliases, mdi } from 'vuetify/iconsets/mdi'
import * as components from 'vuetify/components';
import * as directives from 'vuetify/directives';
import { VCalendar } from 'vuetify/labs/VCalendar';
import { VDateInput } from 'vuetify/labs/VDateInput';
import '@mdi/font/css/materialdesignicons.css';

const vuetify = createVuetify({
  icons: {
    defaultSet: 'mdi',
    aliases,
    sets: {
      mdi,
    },
  },
  components: {
    ...components,
    VCalendar,
    VDateInput,
  },
  directives,
});

createApp(App).use(router).use(createPinia()).use(vuetify).mount('#app');
