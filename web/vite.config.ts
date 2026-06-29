import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { resolve } from 'path'
import http from 'http'
import AutoImport from 'unplugin-auto-import/vite'
import Components from 'unplugin-vue-components/vite'
import { AntDesignVueResolver } from 'unplugin-vue-components/resolvers'

// 启用 keep-alive 的 HTTP Agent，复用 TCP 连接，避免 Windows 下因频繁建连耗尽
// 动态端口导致的 ENOBUFS（No buffer space available）代理错误。
const keepAliveAgent = new http.Agent({
  keepAlive: true,
  keepAliveMsecs: 30000,
  maxSockets: 64,
  maxFreeSockets: 16,
})

// 端口可通过环境变量覆盖（默认 前端 9001 / 后端 9000），便于在本机端口被占用时避让。
// 与 scripts/dev/_common.(sh|ps1) 的 FRONTEND_PORT / BACKEND_PORT 对齐。
const frontendPort = Number(process.env.FRONTEND_PORT) || 9001
const backendTarget = process.env.VITE_BACKEND_URL || `http://127.0.0.1:${process.env.BACKEND_PORT || 9000}`

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    vue(),
    AutoImport({
      resolvers: [AntDesignVueResolver({ importStyle: false })],
      imports: ['vue', 'vue-router', 'pinia'],
      dts: 'src/auto-imports.d.ts',
    }),
    Components({
      resolvers: [AntDesignVueResolver({ importStyle: false })],
      dts: 'src/components.d.ts',
    }),
  ],
  resolve: {
    alias: {
      '@': resolve(__dirname, './src'),
      '@shared': resolve(__dirname, './src/shared'),
    },
  },
  // 预打包常用依赖，避免首次访问时按需编译导致的冷启动延迟
  optimizeDeps: {
    include: [
      'vue',
      'vue-router',
      'pinia',
      'ant-design-vue',
      '@ant-design/icons-vue',
      'axios',
      '@microsoft/signalr',
      'nprogress',
      'echarts',
      'vue-echarts',
    ],
  },
  build: {
    rollupOptions: {
      input: {
        main: resolve(__dirname, 'index.html'),
        mobile: resolve(__dirname, 'mobile.html'),
        redirect: resolve(__dirname, 'redirect.html'),
      },
    },
  },
  server: {
    port: frontendPort,
    strictPort: true, // 端口被占用时报错而非自动切换
    proxy: {
      '/api': {
        target: backendTarget,
        changeOrigin: true,
        proxyTimeout: 300000,
        timeout: 300000,
        agent: keepAliveAgent,
      },
      '/hangfire': {
        target: backendTarget,
        changeOrigin: true,
        proxyTimeout: 30000,
        timeout: 30000,
        agent: keepAliveAgent,
      },
      '/hubs': {
        target: backendTarget,
        changeOrigin: true,
        ws: true,
        proxyTimeout: 60000,
        timeout: 60000,
      },
    },
    // 允许访问上层目录（开发模式 fs 限制）
    fs: {
      allow: ['..'],
    },
    // 文件监听优化，降低 CPU 占用
    watch: {
      usePolling: true,
      interval: 2000,
      ignored: ['**/node_modules/**', '**/.git/**', '**/dist/**'],
    },
    // 启用 HMR（使用稳定的 WebSocket 配置）
    hmr: {
      protocol: 'ws',
      host: 'localhost',
      port: frontendPort,
    },
  },
})
