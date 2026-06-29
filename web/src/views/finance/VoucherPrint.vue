<template>
  <div class="print-root">
    <!-- 工具栏（打印时隐藏） -->
    <div class="print-toolbar no-print">
      <div class="toolbar-left">
        <button class="btn btn-back" @click="goBack">← 返回</button>
        <span class="toolbar-title">凭证打印预览</span>
        <span class="voucher-count">共 {{ vouchers.length }} 张凭证</span>
      </div>
      <div class="toolbar-right">
        <button class="btn btn-pdf" @click="exportPDF">导出PDF</button>
        <button class="btn btn-print" @click="doPrint">打印</button>
      </div>
    </div>

    <!-- 加载中 -->
    <div v-if="loading" class="loading-wrapper no-print">
      <div class="loading-spinner"></div>
      <p>正在加载凭证数据...</p>
    </div>

    <!-- 错误提示 -->
    <div v-else-if="error" class="error-wrapper no-print">
      <p>{{ error }}</p>
      <button class="btn btn-back" @click="goBack">返回</button>
    </div>

    <!-- 凭证内容区 -->
    <div v-else class="print-body">
      <div
        v-for="(voucher, idx) in vouchers"
        :key="voucher.id"
        class="voucher-page"
        :class="{ 'page-break': idx < vouchers.length - 1 }"
      >
        <!-- 凭证标题 -->
        <div class="voucher-title">记 账 凭 证</div>

        <!-- 凭证信息行 -->
        <div class="voucher-meta">
          <div class="meta-item">
            <span class="meta-label">日期：</span>
            <span class="meta-value">{{ formatDateChinese(voucher.date) }}</span>
          </div>
          <div class="meta-item">
            <span class="meta-label">凭证字号：</span>
            <span class="meta-value">{{ voucher.voucherWord }}-{{ String(voucher.voucherNo).padStart(3, '0') }}</span>
          </div>
          <div class="meta-item">
            <span class="meta-label">附件：</span>
            <span class="meta-value">{{ voucher.attachmentCount }} 张</span>
          </div>
        </div>

        <!-- 凭证分录表格 -->
        <table class="voucher-table">
          <thead>
            <tr>
              <th class="col-summary">摘要</th>
              <th class="col-account">会计科目</th>
              <th class="col-amount">借方金额</th>
              <th class="col-amount">贷方金额</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(entry, i) in voucher.entries" :key="entry.FID || entry.id || `entry-${i}`">
              <td class="col-summary">{{ entry.summary }}</td>
              <td class="col-account">
                <span v-if="entry.accountCode">{{ entry.accountCode }}_{{ entry.accountName }}</span>
                <span v-else>{{ entry.accountName }}</span>
              </td>
              <td class="col-amount amount-cell">
                <span v-if="entry.debitAmount">{{ formatAmount(entry.debitAmount) }}</span>
              </td>
              <td class="col-amount amount-cell">
                <span v-if="entry.creditAmount">{{ formatAmount(entry.creditAmount) }}</span>
              </td>
            </tr>
            <!-- 补足空行至最少 6 行，使凭证看起来饱满 -->
            <tr
              v-for="n in Math.max(0, 6 - voucher.entries.length)"
              :key="'empty-' + n"
              class="empty-row"
            >
              <td class="col-summary">&nbsp;</td>
              <td class="col-account">&nbsp;</td>
              <td class="col-amount">&nbsp;</td>
              <td class="col-amount">&nbsp;</td>
            </tr>
          </tbody>
        </table>

        <!-- 合计行 -->
        <div class="voucher-total">
          <div class="total-left">
            <span class="total-label">合计：</span>
            <span class="total-chinese">人民币（大写）{{ numberToChinese(voucher.totalDebit) }}</span>
          </div>
          <div class="total-right">
            <span>借方合计：<strong>{{ formatAmount(voucher.totalDebit) }}</strong></span>
            <span style="margin-left:24px;">贷方合计：<strong>{{ formatAmount(voucher.totalCredit) }}</strong></span>
          </div>
        </div>

        <!-- 签章行 -->
        <div class="voucher-sign">
          <div class="sign-item">
            <span class="sign-label">制单人：</span>
            <span class="sign-value">{{ voucher.creator }}</span>
          </div>
          <div class="sign-item">
            <span class="sign-label">审核人：</span>
            <span class="sign-value">{{ voucher.auditor || '' }}</span>
          </div>
          <div class="sign-item">
            <span class="sign-label">记账：</span>
            <span class="sign-value sign-blank"></span>
          </div>
          <div class="sign-item">
            <span class="sign-label">主管：</span>
            <span class="sign-value sign-blank"></span>
          </div>
        </div>

        <!-- 备注行 -->
        <div class="voucher-remark">
          <span class="remark-label">附注：</span>
          <span class="remark-value">{{ voucher.remark || '' }}</span>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getVoucherDetail } from '@/api/finance'

const route = useRoute()
const router = useRouter()

const loading = ref(true)
const error = ref('')
const vouchers = ref<any[]>([])

// 从路由参数解析凭证 ID 列表
function parseIds(): number[] {
  const raw = route.params.ids as string
  return raw
    .split(',')
    .map(s => parseInt(s.trim(), 10))
    .filter(n => !isNaN(n) && n > 0)
}

// 加载凭证数据
async function loadVouchers() {
  const ids = parseIds()
  if (ids.length === 0) {
    error.value = '未指定凭证 ID，请返回重新选择。'
    loading.value = false
    return
  }

  try {
    const results = await Promise.all(ids.map(id => getVoucherDetail(id)))
    vouchers.value = results.filter(Boolean)
    if (vouchers.value.length === 0) {
      error.value = '未找到指定凭证数据。'
    }
  } catch (e: any) {
    error.value = '加载凭证失败：' + (e?.message || '未知错误')
  } finally {
    loading.value = false
  }
}

// 格式化中文日期
function formatDateChinese(dateStr: string): string {
  if (!dateStr) return ''
  const d = new Date(dateStr)
  if (isNaN(d.getTime())) return dateStr
  return `${d.getFullYear()}年${String(d.getMonth() + 1).padStart(2, '0')}月${String(d.getDate()).padStart(2, '0')}日`
}

// 格式化金额（千分位）
function formatAmount(amount: number | string): string {
  const n = typeof amount === 'string' ? parseFloat(amount) : amount
  if (!n && n !== 0) return ''
  return n.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

// 数字转中文大写金额
function numberToChinese(amount: number | string): string {
  const n = typeof amount === 'string' ? parseFloat(amount) : (amount || 0)
  if (isNaN(n) || n < 0) return '零元整'

  const digits = ['零', '壹', '贰', '叁', '肆', '伍', '陆', '柒', '捌', '玖']
  const units = ['', '拾', '佰', '仟']
  const _bigUnits = ['', '万', '亿', '万亿']
  void _bigUnits

  const intPart = Math.floor(n)
  const decPart = Math.round((n - intPart) * 100)

  if (intPart === 0 && decPart === 0) return '零元整'

  function convertSection(num: number): string {
    if (num === 0) return ''
    let result = ''
    let hasZero = false
    const str = String(num).padStart(4, '0')
    for (let i = 0; i < 4; i++) {
      const d = parseInt(str[i])
      if (d === 0) {
        hasZero = true
      } else {
        if (hasZero && result) result += '零'
        result += digits[d] + units[3 - i]
        hasZero = false
      }
    }
    return result
  }

  let result = ''
  // 亿
  const yi = Math.floor(intPart / 100000000)
  // 万
  const wan = Math.floor((intPart % 100000000) / 10000)
  // 元
  const yuan = intPart % 10000

  if (yi > 0) {
    result += convertSection(yi) + '亿'
    if (wan < 1000 && wan > 0) result += '零'
  }
  if (wan > 0) {
    result += convertSection(wan) + '万'
    if (yuan < 1000 && yuan > 0) result += '零'
  }
  if (yuan > 0) {
    result += convertSection(yuan)
  }

  result += '元'

  // 角分
  if (decPart > 0) {
    const jiao = Math.floor(decPart / 10)
    const fen = decPart % 10
    if (jiao > 0) result += digits[jiao] + '角'
    if (fen > 0) result += digits[fen] + '分'
  } else {
    result += '整'
  }

  return result
}

// 返回
function goBack() {
  router.back()
}

// 打印
function doPrint() {
  window.print()
}

// 导出PDF（借助浏览器打印到PDF）
function exportPDF() {
  window.print()
}

onMounted(() => {
  loadVouchers()
})
</script>

<style scoped>
/* ===== 全局重置（打印页面独占） ===== */
.print-root {
  min-height: 100vh;
  background: var(--bg-page);
  font-family: 'SimSun', '宋体', serif;
}

/* ===== 工具栏 ===== */
.print-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 32px;
  background: var(--topbar-ink);
  color: var(--text-on-accent);
  box-shadow: var(--shadow-md);
  position: sticky;
  top: 0;
  z-index: 100;
}

.toolbar-left {
  display: flex;
  align-items: center;
  gap: 16px;
}

.toolbar-title {
  font-size: 16px;
  font-weight: 600;
  font-family: -apple-system, 'Microsoft YaHei', sans-serif;
}

.voucher-count {
  font-size: 13px;
  color: var(--text-3);
  font-family: -apple-system, 'Microsoft YaHei', sans-serif;
}

.toolbar-right {
  display: flex;
  gap: 12px;
}

.btn {
  padding: 8px 20px;
  border: none;
  border-radius: 4px;
  font-size: 14px;
  cursor: pointer;
  transition: all 0.2s;
  font-family: -apple-system, 'Microsoft YaHei', sans-serif;
}

.btn-back {
  background: transparent;
  color: var(--text-on-accent);
  border: 1px solid var(--border);
}

.btn-back:hover {
  background: color-mix(in srgb, var(--text-on-accent) 10%, transparent);
  border-color: var(--border-strong);
}

.btn-pdf {
  background: var(--color-success);
  color: var(--text-on-accent);
}

.btn-pdf:hover {
  background: color-mix(in srgb, var(--color-success) 88%, #000);
}

.btn-print {
  background: var(--color-primary);
  color: var(--text-on-accent);
}

.btn-print:hover {
  background: var(--color-primary-hover);
}

/* ===== 加载/错误状态 ===== */
.loading-wrapper,
.error-wrapper {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 80px 0;
  color: var(--text-2);
  font-family: -apple-system, 'Microsoft YaHei', sans-serif;
}

.loading-spinner {
  width: 40px;
  height: 40px;
  border: 4px solid var(--border);
  border-top-color: var(--color-primary);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
  margin-bottom: 16px;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

/* ===== 打印内容区 ===== */
.print-body {
  padding: 24px;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 24px;
}

/* ===== 单张凭证 ===== */
.voucher-page {
  width: 210mm;
  min-height: 140mm;
  background: var(--bg-card);
  padding: 12mm 14mm;
  box-shadow: var(--shadow-md);
  box-sizing: border-box;
}

/* ===== 凭证标题 ===== */
.voucher-title {
  text-align: center;
  font-size: 22px;
  font-weight: bold;
  letter-spacing: 8px;
  margin-bottom: 6mm;
  padding-bottom: 4px;
}

/* ===== 凭证信息行 ===== */
.voucher-meta {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 4mm;
  font-size: 13px;
  border-bottom: 1px solid var(--border-strong);
  padding-bottom: 3mm;
}

.meta-item {
  display: flex;
  align-items: center;
}

.meta-label {
  color: var(--text-2);
}

.meta-value {
  font-weight: 500;
}

/* ===== 分录表格 ===== */
.voucher-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 12px;
  margin-bottom: 0;
}

.voucher-table th,
.voucher-table td {
  border: 1px solid var(--border-strong);
  padding: 4px 6px;
  vertical-align: middle;
}

.voucher-table thead th {
  background: var(--bg-muted);
  font-weight: bold;
  text-align: center;
  font-size: 13px;
}

.col-summary {
  width: 30%;
}

.col-account {
  width: 35%;
}

.col-amount {
  width: 17.5%;
  text-align: right;
}

.amount-cell {
  font-family: 'Courier New', 'Consolas', monospace;
  letter-spacing: 0.5px;
}

.empty-row td {
  height: 22px;
}

/* ===== 合计行 ===== */
.voucher-total {
  display: flex;
  justify-content: space-between;
  align-items: center;
  border: 1px solid var(--border-strong);
  border-top: none;
  padding: 4px 6px;
  font-size: 12px;
  background: var(--bg-muted);
}

.total-left {
  display: flex;
  align-items: center;
  gap: 6px;
  flex: 1;
}

.total-label {
  font-weight: bold;
  white-space: nowrap;
}

.total-chinese {
  color: var(--text-1);
}

.total-right {
  white-space: nowrap;
  font-size: 12px;
  font-family: 'Courier New', 'Consolas', monospace;
}

/* ===== 签章行 ===== */
.voucher-sign {
  display: flex;
  justify-content: space-between;
  border: 1px solid var(--border-strong);
  border-top: none;
  padding: 5px 6px;
  font-size: 12px;
}

.sign-item {
  display: flex;
  align-items: center;
  flex: 1;
}

.sign-label {
  white-space: nowrap;
  color: var(--text-2);
}

.sign-value {
  min-width: 48px;
  border-bottom: 1px solid var(--border-strong);
}

.sign-blank {
  display: inline-block;
  min-width: 60px;
}

/* ===== 备注行 ===== */
.voucher-remark {
  border: 1px solid var(--border-strong);
  border-top: none;
  padding: 4px 6px;
  font-size: 12px;
  min-height: 20px;
}

.remark-label {
  color: var(--text-2);
  font-weight: bold;
}

/* ===== 打印样式 ===== */
@media print {
  @page {
    size: A4 portrait;
    margin: 0;
  }

  * {
    -webkit-print-color-adjust: exact;
    print-color-adjust: exact;
  }

  .no-print {
    display: none !important;
  }

  .print-root {
    background: transparent;
  }

  .print-body {
    padding: 0;
    gap: 0;
    align-items: flex-start;
  }

  .voucher-page {
    width: 210mm;
    min-height: 140mm;
    box-shadow: none;
    padding: 10mm 12mm;
    page-break-inside: avoid;
  }

  .voucher-page.page-break {
    page-break-after: always;
  }
}
</style>
