import { defineConfig } from 'vitest/config'
import { resolve } from 'node:path'

// 独立的 vitest 配置：不加载业务 vite.config.ts（含 auto-import / AntD resolver / dev 代理），
// 仅承载纯函数与一致性门禁测试。环境 node（无 DOM），如后续需挂载 .vue 组件再引入 @vitejs/plugin-vue。
// 别名与 vite.config.ts / tsconfig.app.json 的 @、@shared 对齐（vitest 不继承二者的 alias/paths）。
export default defineConfig({
  resolve: {
    alias: {
      '@': resolve(__dirname, './src'),
      '@shared': resolve(__dirname, './src/shared'),
    },
  },
  test: {
    environment: 'node',
    include: ['src/**/*.{test,spec}.ts'],
  },
})
