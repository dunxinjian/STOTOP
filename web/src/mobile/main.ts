import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router'
import { setupErrorReport } from './utils/error-report'

// Vant 全局样式
import 'vant/lib/index.css'
// Vant 变量桥：映射到统一令牌
import '@/styles/vant-bridge.scss'

const app = createApp(App)
const pinia = createPinia()

app.use(pinia)
app.use(router)

// 全局错误监控
setupErrorReport(router)

app.mount('#mobile-app')
