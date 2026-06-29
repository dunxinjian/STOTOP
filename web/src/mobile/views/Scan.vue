<template>
  <div class="page-scan">
    <van-nav-bar title="扫一扫" left-arrow @click-left="router.back()" />

    <div class="scan-body">
      <!-- 扫码入口按钮 -->
      <div class="scan-entry">
        <van-button
          type="primary"
          size="large"
          round
          icon="scan"
          :loading="scanning"
          loading-text="扫码中..."
          @click="handleScan"
        >
          点击扫码
        </van-button>
      </div>

      <!-- 手动输入 -->
      <div class="manual-input">
        <div class="manual-input__label">或直接输入条码:</div>
        <div class="manual-input__row">
          <van-field
            v-model="manualText"
            placeholder="输入条码或内容"
            clearable
            @keyup.enter="handleManualSubmit"
          />
          <van-button type="primary" size="small" @click="handleManualSubmit">
            确定
          </van-button>
        </div>
      </div>

      <!-- 扫码结果 -->
      <div class="scan-result-section" v-if="currentResult">
        <div class="section-title">扫码结果</div>

        <!-- 非信任域名警告 -->
        <van-dialog
          v-model:show="showUntrustedDialog"
          title="安全提示"
          show-cancel-button
          confirm-button-text="仍然打开"
          @confirm="openUntrustedUrl"
        >
          <div class="untrusted-warning">
            <van-icon name="warning-o" color="#ee0a24" size="24" />
            <p>非信任域名，确定要打开吗？</p>
            <p class="url-text" v-text="currentResult.text"></p>
          </div>
        </van-dialog>

        <ScanResult
          :result="currentResult"
          :actions="currentActions"
          @action="handleAction"
        />

        <!-- 继续扫码按钮 -->
        <van-button
          plain
          type="primary"
          size="small"
          icon="scan"
          @click="handleScan"
          class="continue-btn"
        >
          继续扫码
        </van-button>
      </div>

      <!-- 扫码历史 -->
      <div class="scan-history" v-if="history.length > 0">
        <div class="section-title">扫码历史 (最近5条)</div>
        <div
          class="history-item"
          v-for="(item, idx) in history"
          :key="idx"
          @click="processResult(item)"
        >
          <van-tag :type="item.type === 'qrCode' ? 'primary' : item.type === 'barCode' ? 'success' : 'warning'" size="medium">
            {{ item.type === 'qrCode' ? 'QR码' : item.type === 'barCode' ? '条形码' : '文本' }}
          </van-tag>
          <span class="history-item__text" v-text="item.text"></span>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import {
  NavBar as VanNavBar,
  Button as VanButton,
  Field as VanField,
  Tag as VanTag,
  Icon as VanIcon,
  Dialog as VanDialog,
  showToast,
} from 'vant'
import { bridge } from '../utils/dingtalk-bridge'
import ScanResult from '../components/ScanResult.vue'

defineOptions({ name: 'MobileScan' })

const router = useRouter()

const scanning = ref(false)
const manualText = ref('')
const currentResult = ref<{ type: string; text: string } | null>(null)
const showUntrustedDialog = ref(false)

// --- 历史记录 ---
const HISTORY_KEY = 'stotop_scan_history'

function loadHistory(): Array<{ type: string; text: string }> {
  try {
    const raw = localStorage.getItem(HISTORY_KEY)
    return raw ? JSON.parse(raw) : []
  } catch {
    return []
  }
}

function saveHistory(item: { type: string; text: string }) {
  const list = loadHistory()
  // 去重：同text不重复添加
  const filtered = list.filter(h => h.text !== item.text)
  filtered.unshift(item)
  // 最多保留5条
  localStorage.setItem(HISTORY_KEY, JSON.stringify(filtered.slice(0, 5)))
  history.value = filtered.slice(0, 5)
}

const history = ref<Array<{ type: string; text: string }>>(loadHistory())

// --- URL 白名单检查 ---
function isTrustedUrl(url: string): boolean {
  try {
    const parsed = new URL(url)
    const hostname = parsed.hostname
    const currentHost = location.hostname
    if (hostname === currentHost) return true
    if (hostname.endsWith('.dingtalk.com') || hostname === 'dingtalk.com') return true
    return false
  } catch {
    return false
  }
}

// --- 判断结果类型 ---
function isStotopLink(text: string): boolean {
  return text.startsWith('stotop://card/')
}

function isExpressBarcode(text: string): boolean {
  return /^\d{10,20}$/.test(text)
}

function isUrl(text: string): boolean {
  return /^https?:\/\//i.test(text)
}

// --- 根据结果生成操作按钮 ---
const currentActions = computed(() => {
  if (!currentResult.value) return []
  const text = currentResult.value.text

  if (isStotopLink(text)) {
    return [{ label: '跳转', action: 'navigate' }]
  }
  if (isExpressBarcode(text)) {
    return [
      { label: '填入表单', action: 'fillForm' },
      { label: '复制', action: 'copy' },
    ]
  }
  if (isUrl(text)) {
    if (isTrustedUrl(text)) {
      return [
        { label: '打开', action: 'openUrl' },
        { label: '复制', action: 'copy' },
      ]
    } else {
      return [
        { label: '打开(非信任域名)', action: 'openUntrusted' },
        { label: '复制', action: 'copy' },
      ]
    }
  }
  return [{ label: '复制', action: 'copy' }]
})

// --- 处理扫码结果 ---
function processResult(result: { type: string; text: string }) {
  currentResult.value = result
  saveHistory(result)

  // stotop 协议自动跳转
  if (isStotopLink(result.text)) {
    const id = result.text.replace('stotop://card/', '')
    router.push(`/m/card/${id}`)
  }
}

// --- 扫码触发 ---
async function handleScan() {
  scanning.value = true
  try {
    const result = await bridge.scan()
    if (result && result.text) {
      processResult(result)
    }
  } catch (e: any) {
    showToast({ message: '扫码失败: ' + (e.message || '未知错误'), position: 'bottom' })
  } finally {
    scanning.value = false
  }
}

// --- 手动输入 ---
function handleManualSubmit() {
  const text = manualText.value.trim()
  if (!text) {
    showToast({ message: '请输入内容', position: 'bottom' })
    return
  }
  // 手动输入默认为 barCode（如果是纯数字）或 text
  const type = isExpressBarcode(text) ? 'barCode' : 'text'
  processResult({ type, text })
  manualText.value = ''
}

// --- 操作处理 ---
function handleAction(action: string) {
  if (!currentResult.value) return
  const text = currentResult.value.text

  switch (action) {
    case 'navigate': {
      const id = text.replace('stotop://card/', '')
      router.push(`/m/card/${id}`)
      break
    }
    case 'fillForm': {
      router.push({ path: '/m/submit/0', query: { barcode: text } })
      break
    }
    case 'openUrl': {
      window.open(text, '_blank')
      break
    }
    case 'openUntrusted': {
      showUntrustedDialog.value = true
      break
    }
    case 'copy': {
      navigator.clipboard.writeText(text).then(() => {
        showToast({ message: '已复制', position: 'bottom' })
      }).catch(() => {
        showToast({ message: '复制失败', position: 'bottom' })
      })
      break
    }
  }
}

function openUntrustedUrl() {
  if (currentResult.value) {
    window.open(currentResult.value.text, '_blank')
  }
}
</script>

<style scoped lang="scss">
.page-scan {
  min-height: 100vh;
  background: #f5f6f7;
}

.scan-body {
  padding: 16px;
}

.scan-entry {
  display: flex;
  justify-content: center;
  padding: 32px 16px;

  .van-button {
    width: 200px;
    height: 52px;
    font-size: 16px;
  }
}

.manual-input {
  background: #fff;
  border-radius: 8px;
  padding: 12px;
  margin-bottom: 16px;

  &__label {
    font-size: 13px;
    color: #666;
    margin-bottom: 8px;
  }

  &__row {
    display: flex;
    align-items: center;
    gap: 8px;

    .van-field {
      flex: 1;
    }
  }
}

.section-title {
  font-size: 14px;
  font-weight: 600;
  color: #333;
  margin-bottom: 10px;
  padding-left: 4px;
}

.scan-result-section {
  margin-bottom: 16px;
}

.continue-btn {
  margin-top: 8px;
}

.untrusted-warning {
  text-align: center;
  padding: 16px;

  p {
    margin: 8px 0;
    font-size: 14px;
    color: #333;
  }

  .url-text {
    font-size: 12px;
    color: #666;
    word-break: break-all;
  }
}

.scan-history {
  margin-top: 16px;
}

.history-item {
  display: flex;
  align-items: center;
  gap: 8px;
  background: #fff;
  border-radius: 6px;
  padding: 10px 12px;
  margin-bottom: 8px;

  &__text {
    flex: 1;
    font-size: 13px;
    color: #555;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }
}
</style>
