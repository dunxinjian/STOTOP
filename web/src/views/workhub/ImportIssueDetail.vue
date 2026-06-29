<script setup lang="ts">
/**
 * ImportIssueDetail.vue — 导入异常处理专页
 *
 * 展示某个 WorkItem（导入异常类型）的摘要、异常记录表格、操作区。
 * 支持：修正后重跑、标记已处理、转派他人、修正指引跳转。
 */
import { ref, computed, onMounted, watch, h } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { message, Modal } from 'ant-design-vue'
import {
  ReloadOutlined,
  CheckCircleOutlined,
  UserSwitchOutlined,
  RightOutlined,
  ExclamationCircleOutlined,
  InfoCircleOutlined,
  WarningOutlined,
  ClockCircleOutlined,
  FileSearchOutlined,
} from '@ant-design/icons-vue'
import dayjs from 'dayjs'
import relativeTimePlugin from 'dayjs/plugin/relativeTime'
import 'dayjs/locale/zh-cn'
import PageHeader from '@/components/PageHeader.vue'
import {
  getWorkItems,
  getAffectedRows,
  rerunWorkItem,
  skipWorkItem,
  type WorkItem,
  type AffectedRow,
} from '@/api/workhub'

dayjs.extend(relativeTimePlugin)
dayjs.locale('zh-cn')

// ===== 路由 =====
const route = useRoute()
const router = useRouter()
const workItemId = computed(() => Number(route.params.id))

// ===== 数据状态 =====
const workItem = ref<WorkItem | null>(null)
const loadingDetail = ref(false)
const affectedRows = ref<AffectedRow[]>([])
const affectedTotal = ref(0)
const affectedPage = ref(1)
const affectedPageSize = ref(20)
const loadingRows = ref(false)
const rerunLoading = ref(false)
const skipLoading = ref(false)
const isClosed = ref(false)

// ===== 问题类型映射 =====
const issueTypeLabels: Record<string, string> = {
  'OUTLET_UNMATCHED': '网点未匹配',
  'PRICING_RULE_MISSING': '计价规则缺失',
  'VOUCHER_RULE_MISSING': '凭证规则缺失',
  'DATA_FORMAT_ERROR': '数据格式错误',
  'BALANCE_CHECK_FAILED': '借贷不平衡',
  'ORG_BINDPOINT_MISSING': '网点未登记到组织',
}

const issueGuideMap: Record<string, { text: string; route?: string; routeLabel?: string }> = {
  'OUTLET_UNMATCHED': {
    text: '请在基础数据→网点管理中补录缺失网点',
    route: '/express/network-point-aliases',
    routeLabel: '前往网点名称映射',
  },
  'PRICING_RULE_MISSING': {
    text: '请在计价规则配置中添加对应规则',
    route: '/express/quotation',
    routeLabel: '前往快递报价',
  },
  'VOUCHER_RULE_MISSING': {
    text: '请在自动凭证规则中配置',
    route: '/finance/voucher-template',
    routeLabel: '前往凭证模板',
  },
  'DATA_FORMAT_ERROR': {
    text: '请修正源文件后重新上传',
    route: '/cardflow/upload-center',
    routeLabel: '前往上传中心',
  },
  'BALANCE_CHECK_FAILED': {
    text: '凭证借贷不平衡，需人工核实调整',
  },
  'ORG_BINDPOINT_MISSING': {
    text: '网点未登记到组织，请在组织管理中补录',
    route: '/system/organizations',
    routeLabel: '前往组织管理',
  },
}

// ===== 计算属性 =====
const issueType = computed(() => workItem.value?.metadata?.issueType as string || '')
const orgName = computed(() => workItem.value?.metadata?.orgName as string || '')
const batchCount = computed(() => Number(workItem.value?.metadata?.batchCount || 0))
const firstOccurrence = computed(() => workItem.value?.timestamp || '')
const dispatcher = computed(() => workItem.value?.metadata?.dispatcher as string || '')

const issueTypeLabel = computed(() => {
  const t = issueType.value
  return issueTypeLabels[t] || t || '导入异常'
})

const pageTitle = computed(() => {
  const parts = [issueTypeLabel.value]
  if (orgName.value) parts.push(orgName.value)
  return parts.join(' — ')
})

const currentGuide = computed(() => issueGuideMap[issueType.value])

const statusTag = computed(() => {
  if (isClosed.value) return { text: '已关闭', color: 'default' }
  const p = workItem.value?.priority
  if (p === 'urgent') return { text: '紧急', color: 'error' }
  if (p === 'high') return { text: '待处理', color: 'warning' }
  return { text: '待处理', color: 'processing' }
})

// ===== 表格列 =====
const columns = [
  { title: '批次名', dataIndex: 'batchName', key: 'batchName', width: 180, ellipsis: true },
  { title: '原始行号', dataIndex: 'originalRowNumber', key: 'originalRowNumber', width: 100, align: 'center' as const },
  { title: '业务主键', dataIndex: 'bizKey', key: 'bizKey', width: 160, ellipsis: true },
  { title: '错误信息', dataIndex: 'errorMessage', key: 'errorMessage', ellipsis: true },
  { title: '处理状态', dataIndex: 'status', key: 'status', width: 100, align: 'center' as const },
]

const statusColorMap: Record<string, string> = {
  pending: 'orange',
  processing: 'blue',
  resolved: 'green',
  skipped: 'default',
  failed: 'red',
}

const statusLabelMap: Record<string, string> = {
  pending: '待处理',
  processing: '处理中',
  resolved: '已解决',
  skipped: '已跳过',
  failed: '失败',
}

// ===== 数据加载 =====
async function fetchWorkItemDetail() {
  loadingDetail.value = true
  try {
    // 通过列表接口获取单个工作项（复用已有 API）
    const result = await getWorkItems({ page: 1, pageSize: 1 })
    // 尝试从列表中匹配，或直接用全量搜索方式
    // 实际上后端 WorkItem id 与 route.params.id 对应
    // 这里暂用 metadata 匹配，真实场景后端应提供 getById
    if (result?.items) {
      const found = result.items.find(i => String(i.id) === String(workItemId.value))
      if (found) {
        workItem.value = found
      }
    }
  } catch (e) {
    console.warn('[ImportIssueDetail] 获取工作项失败', e)
  } finally {
    loadingDetail.value = false
  }
}

async function fetchAffectedRows() {
  loadingRows.value = true
  try {
    const result = await getAffectedRows(workItemId.value, affectedPage.value, affectedPageSize.value)
    if (result) {
      affectedRows.value = result.items || []
      affectedTotal.value = result.total || 0
    }
  } catch (e) {
    console.warn('[ImportIssueDetail] 获取异常记录失败', e)
  } finally {
    loadingRows.value = false
  }
}

function handleTableChange(pagination: { current?: number; pageSize?: number }) {
  if (pagination.current) affectedPage.value = pagination.current
  if (pagination.pageSize) affectedPageSize.value = pagination.pageSize
  fetchAffectedRows()
}

// ===== 操作 =====

// 修正后重跑
function handleRerun() {
  Modal.confirm({
    title: '确认重跑',
    icon: () => h(ExclamationCircleOutlined),
    content: `将对 ${affectedTotal.value} 条异常记录从 ${issueTypeLabel.value} 阶段重新执行，确认？`,
    okText: '确认重跑',
    cancelText: '取消',
    async onOk() {
      rerunLoading.value = true
      try {
        await rerunWorkItem(workItemId.value)
        message.success('已提交重跑任务，请稍后查看结果')
        fetchAffectedRows()
      } catch {
        message.error('重跑失败，请重试')
      } finally {
        rerunLoading.value = false
      }
    },
  })
}

// 标记为已处理
const skipRemark = ref('')
const skipModalVisible = ref(false)

function handleSkipOpen() {
  skipRemark.value = ''
  skipModalVisible.value = true
}

async function handleSkipConfirm() {
  skipLoading.value = true
  try {
    await skipWorkItem(workItemId.value, skipRemark.value || undefined)
    message.success('已标记为已处理')
    isClosed.value = true
    skipModalVisible.value = false
  } catch {
    message.error('操作失败，请重试')
  } finally {
    skipLoading.value = false
  }
}

// 转派他人
function handleTransfer() {
  // TODO: workhub.ts 中暂无 reassign/transfer API，待后端实现后对接
  message.info('转派功能即将上线')
}

// 跳转到修正指引
function goToGuideRoute() {
  if (currentGuide.value?.route) {
    router.push(currentGuide.value.route)
  }
}

// JSON 格式化
function formatJson(data: any): string {
  if (!data) return '—'
  try {
    return JSON.stringify(data, null, 2)
  } catch {
    return String(data)
  }
}

// 时间格式化
function formatTime(val?: string): string {
  if (!val) return '—'
  return dayjs(val).format('YYYY-MM-DD HH:mm:ss')
}

// ===== 生命周期 =====
onMounted(() => {
  fetchWorkItemDetail()
  fetchAffectedRows()
})

watch(workItemId, () => {
  fetchWorkItemDetail()
  affectedPage.value = 1
  fetchAffectedRows()
})
</script>

<template>
  <div class="import-issue-page">
    <PageHeader :title="pageTitle" backTo="/workhub">
      <template #right>
        <a-tag :color="statusTag.color" class="status-tag">{{ statusTag.text }}</a-tag>
      </template>
    </PageHeader>

    <!-- 摘要卡片区 -->
    <div class="summary-section">
      <a-row :gutter="16">
        <a-col :span="6">
          <a-card size="small" class="stat-card">
            <a-statistic title="影响批次数" :value="batchCount">
              <template #prefix>
                <FileSearchOutlined style="color: var(--color-info)" />
              </template>
            </a-statistic>
          </a-card>
        </a-col>
        <a-col :span="6">
          <a-card size="small" class="stat-card">
            <a-statistic title="异常记录数" :value="affectedTotal" :value-style="{ color: '#fa541c' }"><!-- hex-ok: 保持原异常记录数橙色，仅修类型，不改运行时配色 -->
              <template #prefix>
                <WarningOutlined style="color: #fa541c" />
              </template>
            </a-statistic>
          </a-card>
        </a-col>
        <a-col :span="6">
          <a-card size="small" class="stat-card">
            <a-statistic title="首次出现时间" :value="formatTime(firstOccurrence)" :value-style="{ fontSize: '14px' }">
              <template #prefix>
                <ClockCircleOutlined style="color: var(--color-warning)" />
              </template>
            </a-statistic>
          </a-card>
        </a-col>
        <a-col :span="6">
          <a-card size="small" class="stat-card">
            <a-statistic title="派发人" :value="dispatcher || '系统自动'" :value-style="{ fontSize: '14px' }">
              <template #prefix>
                <UserSwitchOutlined style="color: var(--color-info)" />
              </template>
            </a-statistic>
          </a-card>
        </a-col>
      </a-row>
    </div>

    <!-- 异常记录表格 -->
    <a-card size="small" class="table-card" title="异常记录">
      <a-table
        :columns="columns"
        :data-source="affectedRows"
        :loading="loadingRows"
        :pagination="{
          current: affectedPage,
          pageSize: affectedPageSize,
          total: affectedTotal,
          showSizeChanger: true,
          showTotal: (t: number) => `共 ${t} 条`,
          pageSizeOptions: ['10', '20', '50'],
        }"
        :row-key="(r: AffectedRow) => r.id"
        :scroll="{ x: 800 }"
        size="small"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <a-tag :color="statusColorMap[(record as AffectedRow).status] || 'default'">
              {{ statusLabelMap[(record as AffectedRow).status] || (record as AffectedRow).status }}
            </a-tag>
          </template>
          <template v-if="column.key === 'errorMessage'">
            <a-tooltip :title="(record as AffectedRow).errorMessage">
              {{ (record as AffectedRow).errorMessage }}
            </a-tooltip>
          </template>
        </template>

        <!-- 展开行：完整行数据 JSON -->
        <template #expandedRowRender="{ record }">
          <div class="expanded-row">
            <div class="expanded-label">完整行数据：</div>
            <pre class="expanded-json">{{ formatJson((record as AffectedRow).rawData) }}</pre>
          </div>
        </template>
      </a-table>
    </a-card>

    <!-- 操作区 -->
    <a-card size="small" class="action-card" title="处理操作">
      <!-- 修正指引 -->
      <div v-if="currentGuide" class="guide-section">
        <a-alert type="info" show-icon>
          <template #icon><InfoCircleOutlined /></template>
          <template #message>
            <div class="guide-content">
              <span>{{ currentGuide.text }}</span>
              <a-button
                v-if="currentGuide.route"
                type="link"
                size="small"
                class="guide-link"
                @click="goToGuideRoute"
              >
                {{ currentGuide.routeLabel }}
                <template #icon><RightOutlined /></template>
              </a-button>
            </div>
          </template>
        </a-alert>
      </div>

      <!-- 操作按钮 -->
      <div class="action-buttons">
        <a-space>
          <a-button
            type="primary"
            :loading="rerunLoading"
            :disabled="isClosed"
            @click="handleRerun"
          >
            <template #icon><ReloadOutlined /></template>
            修正后重跑
          </a-button>
          <a-button
            :loading="skipLoading"
            :disabled="isClosed"
            @click="handleSkipOpen"
          >
            <template #icon><CheckCircleOutlined /></template>
            标记为已处理
          </a-button>
          <a-button :disabled="isClosed" @click="handleTransfer">
            <template #icon><UserSwitchOutlined /></template>
            转派他人
          </a-button>
        </a-space>
      </div>
    </a-card>

    <!-- 标记为已处理 Modal -->
    <a-modal
      v-model:open="skipModalVisible"
      title="标记为已处理"
      :confirm-loading="skipLoading"
      ok-text="确认"
      cancel-text="取消"
      @ok="handleSkipConfirm"
    >
      <p style="margin-bottom: 8px; color: rgba(0,0,0,0.65)">请输入处理备注（可选）：</p>
      <a-textarea
        v-model:value="skipRemark"
        placeholder="请简要说明处理情况…"
        :rows="3"
        :maxlength="500"
        show-count
      />
    </a-modal>
  </div>
</template>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.import-issue-page {
  padding: 16px 24px 24px;
  display: flex;
  flex-direction: column;
  gap: 16px;
  max-width: 1200px;
  margin: 0 auto;
}

.status-tag {
  font-size: 13px;
  padding: 2px 12px;
}

// 摘要卡片
.summary-section {
  .stat-card {
    border-radius: $border-radius-lg;
    transition: $transition-base;

    &:hover {
      box-shadow: $shadow-card-hover;
    }
  }
}

// 表格卡片
.table-card {
  border-radius: $border-radius-lg;
}

.expanded-row {
  padding: 8px 0;
}

.expanded-label {
  font-size: 12px;
  color: $text-secondary;
  margin-bottom: 6px;
  font-weight: 600;
}

.expanded-json {
  background: #fafafa;
  border: 1px solid $border-color-lighter;
  border-radius: $border-radius-md;
  padding: 12px;
  font-size: 12px;
  line-height: 1.6;
  max-height: 300px;
  overflow: auto;
  margin: 0;
  white-space: pre-wrap;
  word-break: break-all;
}

// 操作区
.action-card {
  border-radius: $border-radius-lg;
}

.guide-section {
  margin-bottom: 16px;
}

.guide-content {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.guide-link {
  padding: 0;
  height: auto;
  font-size: 13px;
}

.action-buttons {
  display: flex;
  align-items: center;
}
</style>
