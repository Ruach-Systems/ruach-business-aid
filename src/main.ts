import { createApp } from 'vue'
import { createPinia } from 'pinia'
import { IonicVue } from '@ionic/vue'
import App from './App.vue'
import router from './router'
import { useBusinessStore } from './stores/business'
import { setupPwaUpdates } from './services/pwaUpdates'

import '@ionic/vue/css/core.css'
import '@ionic/vue/css/normalize.css'
import '@ionic/vue/css/structure.css'
import '@ionic/vue/css/typography.css'
import '@ionic/vue/css/padding.css'
import '@ionic/vue/css/flex-utils.css'
import './theme/variables.css'
import './styles/app.css'

const app = createApp(App)
const pinia = createPinia()
app.use(IonicVue)
app.use(pinia)
app.use(router)

const store = useBusinessStore(pinia)
void store.bootstrap()
router.isReady().then(() => {
  app.mount('#app')
  setupPwaUpdates()
})
