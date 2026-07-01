import { createApp } from 'vue'
import { createPinia } from 'pinia'
import piniaPersistedState from 'pinia-plugin-persistedstate'
import { VueQueryPlugin } from '@tanstack/vue-query'
import router from './router'
import App from './App.vue'
import { i18n } from './i18n'
import './style.css'

const pinia = createPinia()
pinia.use(piniaPersistedState)

createApp(App)
  .use(pinia)
  .use(router)
  .use(VueQueryPlugin)
  .use(i18n)
  .mount('#app')
