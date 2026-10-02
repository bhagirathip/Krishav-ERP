import { createApp } from 'vue';
import App from './App.vue';
import router from './router';

import './styles/common.css';
import './styles/dashboard.css';
import './styles/opd.css';
import './styles/ipd.css';
import './styles/pharmacy.css';
import './styles/lab.css';
import './styles/staff.css';
import './styles/expense.css';
import './styles/reports.css';
import './styles/settings.css';

createApp(App).use(router).mount('#app');
