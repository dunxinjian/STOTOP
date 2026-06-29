import { createApp } from 'vue'
import { createPinia } from 'pinia'
import Antd from 'ant-design-vue'
import 'ant-design-vue/dist/reset.css'
import router from './router'
import App from './App.vue'
import '@/styles/index.scss'
import dayjs from 'dayjs'
import 'dayjs/locale/zh-cn'
dayjs.locale('zh-cn')

const app = createApp(App)

app.use(createPinia())
app.use(router)
app.use(Antd)

// 全局未捕获异常处理
app.config.errorHandler = (err, instance, info) => {
  console.error('Uncaught error:', err, info)
}

app.mount('#app')
