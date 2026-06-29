<template>
  <div class="period-closing-container">
    <PageHeader title="期末结账">
      <template #left>
        <AccountSetSelector style="width: 200px;" />
      </template>
      <template #right>
        <a-button
          v-if="has(FinancePermissions.PeriodReopen) && accountSetStore.hasAccountSetPermission(AccountSetPermissions.PeriodReopen)"
          :disabled="!closingInfo?.canReopen || operating"
          :loading="reopening"
          @click="handleReopen"
        >
          反结账
        </a-button>
        <a-button
          v-if="has(FinancePermissions.PeriodClose) && accountSetStore.hasAccountSetPermission(AccountSetPermissions.PeriodClose)"
          type="primary"
          :disabled="!canCloseAllowed || operating"
          :loading="closing"
          @click="handleClose"
        >
          结 账
        </a-button>
        <a-button :loading="loading" @click="loadData">刷新</a-button>
      </template>
      <template #toolbar>
        <div style="display:flex; align-items:center; justify-content:space-between; width:100%;">
          <div style="display:flex; align-items:center; gap:8px;">
            <span class="period-label" v-if="closingInfo?.currentPeriod">{{ formatPeriodTitle(closingInfo.currentPeriod) }}</span>
            <span v-else class="no-period">暂无待结账期间</span>
          </div>
          <div></div>
        </div>
      </template>
    </PageHeader>

    <!-- 主内容区 -->
    <a-spin :spinning="loading">
    <div class="main-content">
      <!-- 科目信息表格 -->
      <div class="account-form">
        <div class="form-row">
          <div class="form-label">利润结转至：</div>
          <div class="form-value">
            <a-input
              :value="profitAccountDisplay"
              readonly
              placeholder="未配置"
            />
          </div>
        </div>
        <div class="form-divider" />
        <div class="form-row">
          <div class="form-label">全年利润结转至：</div>
          <div class="form-value">
            <a-input
              :value="retainedEarningsDisplay"
              readonly
              placeholder="未配置"
            />
          </div>
        </div>
      </div>

      <!-- 结账预检区域 -->
      <div v-if="closingInfo?.currentPeriod" class="check-section">
        <div class="check-header">
          <span class="check-title">结账前检查</span>
          <a-button size="small" :loading="checking" @click="runPreCheck">重新检查</a-button>
        </div>
      
        <!-- 检查中 -->
        <div v-if="checking" class="check-loading">
          <LoadingOutlined spin />
          <span>正在检查中...</span>
        </div>
      
        <!-- 检查结果 -->
        <div v-else-if="checkResult" class="check-result">
          <!-- 未审核凭证检查 -->
          <div class="check-item" :class="checkResult.unauditedCount === 0 ? 'check-pass' : 'check-fail'">
            <CheckCircleOutlined v-if="checkResult.unauditedCount === 0" style="color: var(--color-success)" />
            <CloseCircleOutlined v-else style="color: var(--color-danger)" />
            <span>未审核凭证：</span>
            <span :class="checkResult.unauditedCount === 0 ? 'text-success' : 'text-danger'">
              {{ checkResult.unauditedCount }} 张
            </span>
          </div>
      
          <!-- 借贷不平衡检查 -->
          <div class="check-item" :class="checkResult.unbalancedCount === 0 ? 'check-pass' : 'check-fail'">
            <CheckCircleOutlined v-if="checkResult.unbalancedCount === 0" style="color: var(--color-success)" />
            <CloseCircleOutlined v-else style="color: var(--color-danger)" />
            <span>借贷不平衡凭证：</span>
            <span :class="checkResult.unbalancedCount === 0 ? 'text-success' : 'text-danger'">
              {{ checkResult.unbalancedCount }} 张
            </span>
            <span v-if="checkResult.unbalancedCount > 0" class="unbalanced-nos">
              （凭证号：{{ checkResult.unbalancedVoucherNos.join(', ') }}）
            </span>
          </div>
      
          <!-- 试算平衡检查 -->
          <div class="check-item" :class="trialBalanceResult?.isBalanced ? 'check-pass' : (trialBalanceResult ? 'check-fail' : '')">
            <CheckCircleOutlined v-if="trialBalanceResult?.isBalanced" style="color: var(--color-success)" />
            <CloseCircleOutlined v-else-if="trialBalanceResult && !trialBalanceResult.isBalanced" style="color: var(--color-danger)" />
            <LoadingOutlined v-else-if="trialBalanceLoading" spin />
            <span>试算平衡：</span>
            <span v-if="trialBalanceResult" :class="trialBalanceResult.isBalanced ? 'text-success' : 'text-danger'">
              {{ trialBalanceResult.isBalanced ? '平衡' : '不平衡' }}
            </span>
            <span v-else class="text-muted">未检查</span>
            <a-button v-if="!trialBalanceLoading" size="small" type="link" @click="runTrialBalance">
              {{ trialBalanceResult ? '重新检查' : '检查' }}
            </a-button>
          </div>
      
          <!-- 试算平衡明细 -->
          <div v-if="trialBalanceResult" class="trial-balance-detail">
            <div class="trial-balance-summary">
              <span>借方合计：<strong>{{ formatAmount(trialBalanceResult.totalDebit) }}</strong></span>
              <span style="margin-left: 24px;">贷方合计：<strong>{{ formatAmount(trialBalanceResult.totalCredit) }}</strong></span>
              <span v-if="!trialBalanceResult.isBalanced" style="margin-left: 24px; color: var(--color-danger);">
                差额：<strong>{{ formatAmount(Math.abs(trialBalanceResult.totalDebit - trialBalanceResult.totalCredit)) }}</strong>
              </span>
            </div>
            <a-button v-if="trialBalanceResult.details?.length" size="small" type="link" @click="showTrialBalanceModal = true">
              查看明细
            </a-button>
          </div>
      
          <!-- 整体结果 -->
          <div class="check-summary" :class="canCloseAllowed ? 'summary-pass' : 'summary-fail'">
            <CheckCircleFilled v-if="canCloseAllowed" style="color: var(--color-success)" />
            <ExclamationCircleFilled v-else style="color: var(--color-danger)" />
            <span v-if="canCloseAllowed">所有检查通过，可以结账</span>
            <span v-else>存在问题，请先处理以下问题再结账</span>
          </div>
      
          <!-- 问题清单 -->
          <ul v-if="checkResult.messages?.length" class="check-messages">
            <li v-for="(msg, idx) in checkResult.messages" :key="idx" class="check-message-item">
              {{ msg }}
            </li>
          </ul>
        </div>
      
        <!-- 未检查提示 -->
        <div v-else class="check-placeholder">点击“重新检查”按钮开始结账前检查</div>
      </div>

      <!-- 提示信息 -->
      <div class="tips-section">
        <a-alert
          type="info"
          :closable="false"
          showIcon
        >
          <template #message>
            <div>结账将自动生成收益凭证及损失凭证；</div>
            <div>
              12月结账，将自动将
              <strong>{{ profitAccountDisplay || '3103_本年利润' }}</strong>
              结转至
              <strong>{{ retainedEarningsDisplay || '310405_未分配利润' }}</strong>。
            </div>
            <div>结账后该期间凭证将被锁定，无法修改或删除。</div>
          </template>
        </a-alert>

        <a-alert
          type="warning"
          :closable="false"
          showIcon
          style="margin-top: 8px;"
        >
          <template #message>
            <strong>请确切3103，310405科目存在，否则将不能正常结账</strong>
          </template>
        </a-alert>

        <!-- 额外警告信息 -->
        <a-alert
          v-if="closingInfo?.message"
          type="error"
          :closable="false"
          showIcon
          :message="closingInfo.message"
          style="margin-top: 8px;"
        />
      </div>
    </div>
    </a-spin>

    <!-- 试算平衡明细弹窗 -->
    <a-modal
      v-model:open="showTrialBalanceModal"
      title="试算平衡明细"
      :footer="null"
      width="700px"
    >
      <div v-if="trialBalanceResult" style="max-height: 400px; overflow-y: auto;">
        <a-table
          :dataSource="trialBalanceResult.details"
          :pagination="false"
          size="small"
          rowKey="accountId"
          :columns="[
            { title: '科目编码', dataIndex: 'accountCode', width: 120 },
            { title: '科目名称', dataIndex: 'accountName', width: 200 },
            { title: '借方余额', dataIndex: 'debitBalance', width: 140, align: 'right' },
            { title: '贷方余额', dataIndex: 'creditBalance', width: 140, align: 'right' },
          ]"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.dataIndex === 'debitBalance'">
              {{ formatAmount(record.debitBalance) }}
            </template>
            <template v-else-if="column.dataIndex === 'creditBalance'">
              {{ formatAmount(record.creditBalance) }}
            </template>
          </template>
        </a-table>
        <div style="margin-top: 12px; padding: 8px 12px; background: #fafafa; border-radius: 4px; display: flex; justify-content: space-between;">
          <span>借方合计：<strong>{{ formatAmount(trialBalanceResult.totalDebit) }}</strong></span>
          <span>贷方合计：<strong>{{ formatAmount(trialBalanceResult.totalCredit) }}</strong></span>
          <span :style="{ color: trialBalanceResult.isBalanced ? 'var(--color-success)' : 'var(--color-danger)' }">
            {{ trialBalanceResult.isBalanced ? '平衡' : '不平衡' }}
          </span>
        </div>
      </div>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { message, Modal } from 'ant-design-vue'
import {
  CheckCircleOutlined, CloseCircleOutlined, CheckCircleFilled,
  ExclamationCircleFilled, LoadingOutlined
} from '@ant-design/icons-vue'
import { getClosingInfo, closePeriod, reopenPeriod, preCloseCheck, generateTrialBalance } from '@/api/finance'
import { useAccountSetStore } from '@/stores/accountSet'
import { usePermission, FinancePermissions } from '@/utils/permission'
import { AccountSetPermissions } from '@/constants/accountSetPermissions'
import AccountSetSelector from '@/components/AccountSetSelector.vue'
import PageHeader from '@/components/PageHeader.vue'

const accountSetStore = useAccountSetStore()
const { has } = usePermission()

const loading = ref(false)
const closing = ref(false)
const reopening = ref(false)
const checking = ref(false)
const closingInfo = ref<any>(null)
const checkResult = ref<any>(null)
const trialBalanceResult = ref<any>(null)
const trialBalanceLoading = ref(false)
const showTrialBalanceModal = ref(false)

const operating = computed(() => closing.value || reopening.value)

// 结账按钮是否可用：需要 canClose 且预检通过且试算平衡
const canCloseAllowed = computed(() => {
  if (!closingInfo.value?.canClose) return false
  if (!checkResult.value) return false
  if (checkResult.value.canClose !== true) return false
  if (!trialBalanceResult.value) return false
  return trialBalanceResult.value.isBalanced === true
})

// 利润结转科目显示
const profitAccountDisplay = computed(() => {
  const acc = closingInfo.value?.profitAccount
  if (!acc) return ''
  return `${acc.code}_${acc.name}`
})

// 全年利润结转科目显示
const retainedEarningsDisplay = computed(() => {
  const acc = closingInfo.value?.retainedEarningsAccount
  if (!acc) return ''
  return `${acc.code}_${acc.name}`
})

function formatPeriodTitle(period: any): string {
  if (!period) return ''
  if (period.periodName) {
    const parts = period.periodName.split('-')
    if (parts.length >= 2) {
      return `${parts[0]}年${String(parseInt(parts[1])).padStart(2, '0')}月`
    }
    return period.periodName
  }
  if (period.year && period.month) {
    return `${period.year}年${String(period.month).padStart(2, '0')}月`
  }
  return ''
}

async function loadData() {
  loading.value = true
  try {
    const accountSetId = accountSetStore.getCurrentAccountSetId() || undefined
    const res = await getClosingInfo(accountSetId) as any
    closingInfo.value = res?.data ?? res ?? null
    checkResult.value = null
    trialBalanceResult.value = null
    if (closingInfo.value?.currentPeriod) {
      await runPreCheck()
    }
  } catch (e: any) {
    message.error(e?.message || '获取结账信息失败')
  } finally {
    loading.value = false
  }
}

async function runPreCheck() {
  const period = closingInfo.value?.currentPeriod
  if (!period) return

  checking.value = true
  try {
    const accountSetId = accountSetStore.getCurrentAccountSetId() || 0
    const year = period.year
    const periodNo = period.month
    const res = await preCloseCheck(accountSetId, year, periodNo) as any
    checkResult.value = res?.data ?? res ?? null
  } catch (e: any) {
    message.error(e?.message || '结账前检查失败')
    checkResult.value = null
  } finally {
    checking.value = false
  }
}

async function runTrialBalance() {
  const period = closingInfo.value?.currentPeriod
  if (!period) return

  trialBalanceLoading.value = true
  try {
    const accountSetId = accountSetStore.getCurrentAccountSetId() || 0
    const res = await generateTrialBalance(period.id, accountSetId) as any
    trialBalanceResult.value = res?.data ?? res ?? null
  } catch (e: any) {
    message.error(e?.message || '试算平衡检查失败')
    trialBalanceResult.value = null
  } finally {
    trialBalanceLoading.value = false
  }
}

function formatAmount(val: number): string {
  if (val == null) return '0.00'
  return Number(val).toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

async function handleClose() {
  const period = closingInfo.value?.currentPeriod
  if (!period) return

  const periodTitle = formatPeriodTitle(period)
  Modal.confirm({
    title: '确认结账',
    content: `确认要对 ${periodTitle} 进行结账操作吗？结账后将自动生成收益/损失凭证，且该期间凭证将被锁定。`,
    okText: '确认结账',
    cancelText: '取消',
    onOk: async () => {
      closing.value = true
      try {
        const accountSetId = accountSetStore.getCurrentAccountSetId() || undefined
        await closePeriod(period.id, accountSetId)
        message.success(`${periodTitle} 结账成功`)
        await loadData()
      } catch (e: any) {
        message.error(e?.message || '结账失败')
      } finally {
        closing.value = false
      }
    }
  })
}

async function handleReopen() {
  const period = closingInfo.value?.lastClosedPeriod
  if (!period) return

  const periodTitle = formatPeriodTitle(period)
  Modal.confirm({
    title: '确认反结账',
    content: `确认要对 ${periodTitle} 进行反结账操作吗？`,
    okText: '确认反结账',
    cancelText: '取消',
    onOk: async () => {
      reopening.value = true
      try {
        const accountSetId = accountSetStore.getCurrentAccountSetId() || undefined
        await reopenPeriod(period.id, accountSetId)
        message.success(`${periodTitle} 反结账成功`)
        await loadData()
      } catch (e: any) {
        message.error(e?.message || '反结账失败')
      } finally {
        reopening.value = false
      }
    }
  })
}

// 监听账套切换
watch(
  () => accountSetStore.currentAccountSetId,
  () => {
    loadData()
  }
)

onMounted(() => {
  loadData()
})
</script>

<style scoped>
.period-closing-container {
  padding: 0;
  background: #fff;
  min-height: 100%;
}

.period-label {
  font-size: 16px;
  font-weight: 500;
  color: #303133;
}

.no-period {
  color: #909399;
  font-size: 14px;
}

.main-content {
  padding: 20px;
}

.account-form {
  border: 1px solid #dcdfe6;
  border-radius: 4px;
  overflow: hidden;
  margin-bottom: 20px;
}

.form-row {
  display: flex;
  align-items: center;
  min-height: 48px;
  padding: 0;
}

.form-label {
  width: 160px;
  min-width: 160px;
  padding: 0 16px;
  font-size: 14px;
  color: #606266;
  background: #fafafa;
  border-right: 1px solid #dcdfe6;
  display: flex;
  align-items: center;
  align-self: stretch;
}

.form-value {
  flex: 1;
  padding: 8px 12px;
}

.form-divider {
  height: 1px;
  background: #dcdfe6;
}

.check-section {
  border: 1px solid #dcdfe6;
  border-radius: 4px;
  margin-bottom: 20px;
  overflow: hidden;
}

.check-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 16px;
  background: #f5f7fa;
  border-bottom: 1px solid #dcdfe6;
}

.check-title {
  font-size: 14px;
  font-weight: 500;
  color: #303133;
}

.check-loading {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 16px;
  color: #606266;
  font-size: 14px;
}

.check-result {
  padding: 12px 16px;
}

.check-item {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 0;
  font-size: 14px;
  border-bottom: 1px solid #f2f2f2;
}

.check-item:last-of-type {
  border-bottom: none;
}

.text-success { color: var(--color-success); font-weight: 500; }
.text-danger { color: var(--color-danger); font-weight: 500; }

.unbalanced-nos {
  color: var(--color-warning);
  font-size: 12px;
  margin-left: 4px;
}

.check-summary {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 12px;
  margin-top: 12px;
  border-radius: 4px;
  font-size: 14px;
  font-weight: 500;
}

.summary-pass { background: var(--color-success-light); color: var(--color-success); }
.summary-fail { background: var(--color-danger-light); color: var(--color-danger); }

.check-messages {
  margin: 8px 0 0 0;
  padding: 10px 12px;
  background: var(--color-danger-light);
  border-radius: 4px;
  list-style: none;
}

.check-message-item {
  font-size: 13px;
  color: var(--color-danger);
  padding: 2px 0;
}

.check-message-item::before { content: '• '; }

.text-muted { color: #909399; }

.trial-balance-detail {
  padding: 8px 12px;
  margin-top: 4px;
  background: #f9f9f9;
  border-radius: 4px;
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.trial-balance-summary {
  font-size: 13px;
  color: #606266;
}

.check-placeholder {
  padding: 16px;
  color: #909399;
  font-size: 14px;
  text-align: center;
}

.tips-section {
  margin-top: 4px;
}
</style>
