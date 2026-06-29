<script setup lang="ts">
/**
 * DelegationPage.vue — 代审批委托管理页
 *
 * 路由：/cardflow/delegations  权限：* 所有登录用户
 * 用户可以在出差或不在线时，将自己名下的待审批任务委托给同事代为处理。
 */
import { ref, reactive, computed, onMounted, h } from 'vue'
import { message } from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import {
  PlusOutlined,
  EditOutlined,
  CloseCircleOutlined,
  TeamOutlined,
} from '@ant-design/icons-vue'
import dayjs, { type Dayjs } from 'dayjs'
import PageHeader from '@/components/PageHeader.vue'
import StatusTag from '@/components/StatusTag.vue'
import {
  getMyDelegations,
  createDelegation,
  updateDelegation,
  cancelDelegation,
  getFlowDefinitions,
} from '@/api/cardflow'
import type {
  DelegationDto,
  CreateDelegationRequest,
  UpdateDelegationRequest,
  FlowDefinitionDto,
} from '@/types/cardflow'
import { getUserList } from '@/api/system'

// ==================== 类型 ====================

type DelegationStatus = 'active' | 'expired' | 'cancelled'

interface StatusMeta {
  text: string
  type: 'success' | 'default' | 'danger'
}

const STATUS_META: Record<DelegationStatus, StatusMeta> = {
  active: { text: '生效中', type: 'success' },
  expired: { text: '已过期', type: 'default' },
  cancelled: { text: '已取消', type: 'danger' },
}

interface UserOption {
  id: number
  name: string
  account?: string
}

// ==================== 列表数据 ====================

const loading = ref(false)
const dataSource = ref<DelegationDto[]>([])
const flowList = ref<FlowDefinitionDto[]>([])

const flowMap = computed(() => {
  const m = new Map<number, FlowDefinitionDto>()
  flowList.value.forEach(f => m.set(f.id, f))
  return m
})

async function loadData() {
  loading.value = true
  try {
    const res = await getMyDelegations()
    dataSource.value = (res as DelegationDto[]) || []
  } catch {
    message.error('加载委托列表失败')
  } finally {
    loading.value = false
  }
}

async function loadFlows() {
  try {
    const res = await getFlowDefinitions({ page: 1, pageSize: 200, status: 'published' })
    flowList.value = res?.items || []
  } catch {
    /* 静默 */
  }
}

// ==================== Modal & 表单 ====================

interface DelegationFormState {
  trusteeId: number | undefined
  trusteeName: string
  range: [Dayjs, Dayjs] | undefined
  applicableFlowIds: number[]
  applyAll: boolean
}

const modalVisible = ref(false)
const modalMode = ref<'create' | 'edit'>('create')
const editingId = ref<number | null>(null)
const submitting = ref(false)

const formRef = ref<any>(null)
const formState = reactive<DelegationFormState>({
  trusteeId: undefined,
  trusteeName: '',
  range: undefined,
  applicableFlowIds: [],
  applyAll: true,
})

const formRules = {
  trusteeId: [{ required: true, message: '请选择受托人' }],
  range: [{ required: true, message: '请选择委托起止时间' }],
}

// 受托人候选
const userOptions = ref<UserOption[]>([])
const userSearchLoading = ref(false)
let userSearchTimer: number | null = null

async function searchUsers(keyword: string) {
  userSearchLoading.value = true
  try {
    const res: any = await getUserList({ keyword: keyword || '', page: 1, pageSize: 30 })
    const items = res?.items || res?.data?.items || res || []
    userOptions.value = (Array.isArray(items) ? items : []).map((u: any) => ({
      id: u.id ?? u.userId,
      name: u.name || u.userName || u.realName || u.account || '',
      account: u.account,
    }))
  } catch {
    userOptions.value = []
  } finally {
    userSearchLoading.value = false
  }
}

function handleUserSearch(keyword: string) {
  if (userSearchTimer) window.clearTimeout(userSearchTimer)
  userSearchTimer = window.setTimeout(() => searchUsers(keyword), 300)
}

function handleTrusteeChange(value: any, option: any) {
  formState.trusteeId = typeof value === 'number' ? value : Number(value)
  formState.trusteeName = (Array.isArray(option) ? option[0]?.label : option?.label) || ''
}

function openCreate() {
  modalMode.value = 'create'
  editingId.value = null
  formState.trusteeId = undefined
  formState.trusteeName = ''
  formState.range = undefined
  formState.applicableFlowIds = []
  formState.applyAll = true
  modalVisible.value = true
  searchUsers('')
}

function openEdit(record: DelegationDto) {
  modalMode.value = 'edit'
  editingId.value = record.id
  formState.trusteeId = record.trusteeId
  formState.trusteeName = record.trusteeName
  formState.range = [dayjs(record.startTime), dayjs(record.endTime)]

  let ids: number[] = []
  if (record.applicableFlowsJson) {
    try {
      const parsed = JSON.parse(record.applicableFlowsJson)
      if (Array.isArray(parsed)) ids = parsed.map((v: any) => Number(v)).filter(n => !isNaN(n))
    } catch {
      /* 忽略解析错误 */
    }
  }
  formState.applicableFlowIds = ids
  formState.applyAll = ids.length === 0

  // 在编辑时若 trustee 不在 options 中，先填入展示
  userOptions.value = [{ id: record.trusteeId, name: record.trusteeName }]
  modalVisible.value = true
  searchUsers('')
}

async function handleSubmit() {
  try {
    await formRef.value?.validate()
  } catch {
    return
  }
  if (!formState.range || formState.range.length !== 2) {
    message.warning('请选择委托起止时间')
    return
  }
  const [startTime, endTime] = formState.range as [Dayjs, Dayjs]
  if (endTime.isBefore(startTime)) {
    message.warning('失效时间不能早于生效时间')
    return
  }

  const flowsJson = formState.applyAll
    ? null
    : formState.applicableFlowIds.length > 0
      ? JSON.stringify(formState.applicableFlowIds)
      : null

  submitting.value = true
  try {
    if (modalMode.value === 'create') {
      const payload: CreateDelegationRequest = {
        trusteeId: formState.trusteeId!,
        trusteeName: formState.trusteeName,
        startTime: startTime.toISOString(),
        endTime: endTime.toISOString(),
        applicableFlowsJson: flowsJson,
      }
      await createDelegation(payload)
      message.success('委托已创建')
    } else if (editingId.value != null) {
      const payload: UpdateDelegationRequest = {
        trusteeId: formState.trusteeId,
        trusteeName: formState.trusteeName,
        startTime: startTime.toISOString(),
        endTime: endTime.toISOString(),
        applicableFlowsJson: flowsJson,
      }
      await updateDelegation(editingId.value, payload)
      message.success('委托已更新')
    }
    modalVisible.value = false
    loadData()
  } catch {
    message.error(modalMode.value === 'create' ? '创建失败' : '更新失败')
  } finally {
    submitting.value = false
  }
}

// ==================== 取消委托 ====================

async function handleCancel(record: DelegationDto) {
  try {
    await cancelDelegation(record.id)
    message.success('已取消委托')
    loadData()
  } catch {
    message.error('取消失败')
  }
}

// ==================== 工具方法 ====================

function statusOf(record: DelegationDto): DelegationStatus {
  const raw = (record.status || '').toLowerCase()
  if (raw === 'cancelled' || raw === 'canceled') return 'cancelled'
  const now = dayjs()
  if (now.isAfter(dayjs(record.endTime))) return 'expired'
  return 'active'
}

function formatTime(val?: string | null) {
  if (!val) return '-'
  return dayjs(val).format('YYYY-MM-DD HH:mm')
}

function applicableFlowsText(record: DelegationDto): string {
  if (!record.applicableFlowsJson) return '全部流程'
  try {
    const arr = JSON.parse(record.applicableFlowsJson) as any[]
    if (!Array.isArray(arr) || arr.length === 0) return '全部流程'
    const names = arr
      .map(id => flowMap.value.get(Number(id))?.flowName)
      .filter(Boolean) as string[]
    if (names.length === 0) return `${arr.length} 个流程`
    if (names.length <= 2) return names.join('、')
    return `${names.slice(0, 2).join('、')} 等 ${names.length} 个流程`
  } catch {
    return '全部流程'
  }
}

// ==================== 表格列 ====================

const columns: TableColumnsType = [
  { title: '受托人', dataIndex: 'trusteeName', key: 'trusteeName', width: 180, ellipsis: true },
  { title: '生效时间', dataIndex: 'startTime', key: 'startTime', width: 170 },
  { title: '失效时间', dataIndex: 'endTime', key: 'endTime', width: 170 },
  { title: '适用流程', key: 'applicableFlows', ellipsis: true },
  { title: '状态', key: 'status', width: 110, align: 'center' },
  { title: '操作', key: 'action', width: 180, fixed: 'right' },
]

// ==================== 生命周期 ====================

onMounted(async () => {
  await Promise.all([loadFlows(), loadData()])
})

// 用于 ExclamationCircleOutlined 引用（避免未使用警告，方便扩展）
void h
</script>

<template>
  <div class="page-container">
    <PageHeader>
      <template #actions>
        <a-space :size="8">
          <a-button type="primary" @click="openCreate">
            <template #icon><PlusOutlined /></template>
            新建委托
          </a-button>
        </a-space>
      </template>
    </PageHeader>

    <a-table
      class="delegation-table"
      :columns="columns"
      :data-source="dataSource"
      :loading="loading"
      row-key="id"
      size="middle"
      :pagination="false"
      :scroll="{ x: 960 }"
      :row-class-name="() => 'delegation-row'"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'trusteeName'">
          <div class="trustee-cell">
            <a-avatar size="small" class="trustee-avatar">
              {{ (record.trusteeName || '?').slice(0, 1) }}
            </a-avatar>
            <span class="trustee-name">{{ record.trusteeName }}</span>
          </div>
        </template>

        <template v-else-if="column.dataIndex === 'startTime'">
          {{ formatTime(record.startTime) }}
        </template>

        <template v-else-if="column.dataIndex === 'endTime'">
          {{ formatTime(record.endTime) }}
        </template>

        <template v-else-if="column.key === 'applicableFlows'">
          <span :class="record.applicableFlowsJson ? '' : 'text-muted'">
            {{ applicableFlowsText(record as DelegationDto) }}
          </span>
        </template>

        <template v-else-if="column.key === 'status'">
          <StatusTag :type="STATUS_META[statusOf(record as DelegationDto)].type">
            {{ STATUS_META[statusOf(record as DelegationDto)].text }}
          </StatusTag>
        </template>

        <template v-else-if="column.key === 'action'">
          <div class="row-actions">
            <a-button
              type="link"
              size="small"
              :disabled="statusOf(record as DelegationDto) !== 'active'"
              @click="openEdit(record as DelegationDto)"
            >
              <EditOutlined /> 编辑
            </a-button>
            <a-popconfirm
              title="确定取消该委托规则？"
              ok-text="确定"
              cancel-text="取消"
              :disabled="statusOf(record as DelegationDto) !== 'active'"
              @confirm="handleCancel(record as DelegationDto)"
            >
              <a-button
                type="link"
                size="small"
                danger
                :disabled="statusOf(record as DelegationDto) !== 'active'"
              >
                <CloseCircleOutlined /> 取消
              </a-button>
            </a-popconfirm>
          </div>
        </template>
      </template>

      <template #emptyText>
        <div class="empty-guide">
          <div class="empty-icon">
            <TeamOutlined />
          </div>
          <div class="empty-title">您还没有设置委托规则</div>
          <div class="empty-tip">出差时可让同事代为审批，期间您的待办将自动转给受托人。</div>
          <a-button type="primary" @click="openCreate">
            <template #icon><PlusOutlined /></template>
            新建委托
          </a-button>
        </div>
      </template>
    </a-table>

    <!-- 新建 / 编辑 Modal -->
    <a-modal
      v-model:open="modalVisible"
      :title="modalMode === 'create' ? '新建委托' : '编辑委托'"
      :width="520"
      :confirm-loading="submitting"
      :mask-closable="false"
      ok-text="确定"
      cancel-text="取消"
      @ok="handleSubmit"
    >
      <a-form
        ref="formRef"
        :model="formState"
        :rules="formRules"
        layout="vertical"
        class="delegation-form"
      >
        <a-form-item label="受托人" name="trusteeId">
          <a-select
            v-model:value="formState.trusteeId"
            show-search
            placeholder="搜索同事姓名 / 账号"
            :filter-option="false"
            :loading="userSearchLoading"
            :options="userOptions.map(u => ({ value: u.id, label: u.name }))"
            @search="handleUserSearch"
            @change="handleTrusteeChange"
          />
        </a-form-item>

        <a-form-item label="委托时间" name="range">
          <a-range-picker
            v-model:value="formState.range"
            show-time
            :placeholder="['生效时间', '失效时间']"
            style="width: 100%"
            format="YYYY-MM-DD HH:mm"
          />
        </a-form-item>

        <a-form-item label="适用流程">
          <a-checkbox v-model:checked="formState.applyAll" class="apply-all-checkbox">
            全部流程
          </a-checkbox>
          <a-select
            v-model:value="formState.applicableFlowIds"
            mode="multiple"
            allow-clear
            placeholder="选择适用流程（不选则代表全部流程）"
            :disabled="formState.applyAll"
            :max-tag-count="3"
            :options="flowList.map(f => ({ value: f.id, label: f.flowName }))"
            style="width: 100%; margin-top: 8px"
          />
          <div class="form-help">
            勾选「全部流程」时，受托人可处理您所有待审批的卡片。
          </div>
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<style scoped lang="scss">
.page-container {
  padding: 0;
}

.delegation-table {
  :deep(.delegation-row .row-actions) {
    opacity: 0.65;
    transition: opacity 0.15s ease;
  }
  :deep(.delegation-row:hover .row-actions) {
    opacity: 1;
  }
}

.row-actions {
  display: inline-flex;
  align-items: center;
  gap: 0;
  flex-wrap: nowrap;

  :deep(.ant-btn-link) {
    padding-inline: 6px;
  }
}

.trustee-cell {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
}

.trustee-avatar {
  background: linear-gradient(135deg, var(--color-info) 0%, color-mix(in srgb, var(--color-info) 65%, transparent) 100%);
  color: var(--text-on-accent);
  font-weight: 600;
  flex: 0 0 auto;
}

.trustee-name {
  font-weight: 500;
  color: var(--text-1);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.text-muted {
  color: var(--text-3);
}

.empty-guide {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 56px 16px;
  color: var(--text-2);

  .empty-icon {
    font-size: 56px;
    color: var(--text-3);
    margin-bottom: 16px;
  }

  .empty-title {
    font-size: 16px;
    font-weight: 600;
    color: var(--text-1);
    margin-bottom: 6px;
  }

  .empty-tip {
    font-size: 13px;
    color: var(--text-3);
    margin-bottom: 20px;
  }
}

.delegation-form {
  .apply-all-checkbox {
    user-select: none;
  }
}

.form-help {
  margin-top: 6px;
  font-size: 12px;
  color: var(--text-3);
  line-height: 1.5;
}
</style>
