<template>
  <div class="page-container">
    <PageHeader title="租户管理" description="平台层管理所有租户">
      <template #left>
        <a-segmented v-model:value="navKey" :options="navOptions" @change="onNav" />
        <a-input-search
          v-model:value="keyword"
          placeholder="名称 / 编号"
          allow-clear
          size="middle"
          style="width: 200px"
          @search="() => {}"
        />
        <a-select
          v-model:value="statusFilter"
          placeholder="全部状态"
          allow-clear
          size="middle"
          style="width: 130px"
          :options="TENANT_STATUS_OPTIONS"
        />
        <a-button size="middle" @click="fetchList">
          <template #icon><ReloadOutlined /></template>刷新
        </a-button>
      </template>
      <template #right>
        <a-button type="primary" size="middle" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新建租户
        </a-button>
      </template>
    </PageHeader>

    <div class="page-card">
      <DataTable
        v-model:pagination="pagination"
        :columns="columns"
        :data-source="displayData"
        :loading="loading"
        :scroll="{ x: 1000 }"
        row-key="id"
        empty-text="暂无租户数据"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'status'">
            <StatusTag :type="statusType(record.status)" dot>{{ record.statusName }}</StatusTag>
          </template>
          <template v-if="column.dataIndex === 'accountSetBindMode'">
            {{ labelOf(ACCOUNT_SET_BIND_MODE_OPTIONS, record.accountSetBindMode) }}
          </template>
          <template v-if="column.dataIndex === 'defaultTodoChannel'">
            {{ labelOf(TODO_CHANNEL_OPTIONS, record.defaultTodoChannel) }}
          </template>
          <template v-if="column.dataIndex === 'expireAt'">
            {{ record.expireAt ? String(record.expireAt).slice(0, 10) : '-' }}
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-dropdown>
              <a-button type="link" size="small">
                更改状态<DownOutlined />
              </a-button>
              <template #overlay>
                <a-menu>
                  <a-menu-item
                    v-for="opt in TENANT_STATUS_OPTIONS"
                    :key="opt.value"
                    :disabled="opt.value === record.status"
                    @click="changeStatus(record as TenantDto, opt.value)"
                  >
                    设为{{ opt.label }}
                  </a-menu-item>
                </a-menu>
              </template>
            </a-dropdown>
            <a-button type="link" size="small" @click="goSubscriptions(record as TenantDto)">开通/续费</a-button>
          </template>
        </template>
      </DataTable>
    </div>

    <a-modal
      v-model:open="dialogVisible"
      title="新建租户"
      :width="600"
      :destroyOnClose="true"
    >
      <a-form ref="formRef" :model="formData" :rules="formRules" layout="vertical" class="modal-form">
        <div class="form-section-title">租户</div>
        <div class="form-grid">
          <a-form-item label="名称" name="name">
            <a-input v-model:value="formData.name" placeholder="请输入租户名称" :maxlength="100" />
          </a-form-item>
          <a-form-item label="编号" name="code">
            <a-input v-model:value="formData.code" placeholder="唯一编号，如 TCMS" :maxlength="50" />
          </a-form-item>
        </div>
        <div class="form-grid">
          <a-form-item label="账套绑定模式" name="accountSetBindMode">
            <a-select v-model:value="formData.accountSetBindMode" :options="ACCOUNT_SET_BIND_MODE_OPTIONS" />
          </a-form-item>
          <a-form-item label="默认待办渠道" name="defaultTodoChannel">
            <a-select v-model:value="formData.defaultTodoChannel" :options="TODO_CHANNEL_OPTIONS" />
          </a-form-item>
        </div>
        <div class="form-grid">
          <a-form-item label="套餐（可选）" name="planId">
            <a-select
              v-model:value="formData.planId"
              placeholder="不选则暂不绑定"
              allow-clear
              :options="planOptions"
            />
          </a-form-item>
          <a-form-item label="到期时间（可选）" name="expireAt">
            <a-date-picker v-model:value="formData.expireAt" valueFormat="YYYY-MM-DD" style="width: 100%" />
          </a-form-item>
        </div>

        <div class="form-section-title">组织根节点</div>
        <div class="form-grid">
          <a-form-item label="根组织名称" name="rootOrgName">
            <a-input v-model:value="formData.rootOrgName" placeholder="如 太仓美申" :maxlength="100" />
          </a-form-item>
          <a-form-item label="根组织类别" name="rootOrgKind">
            <a-select v-model:value="formData.rootOrgKind" :options="ROOT_ORG_KIND_OPTIONS" />
          </a-form-item>
        </div>

        <div class="form-section-title">初始管理员</div>
        <div class="form-grid">
          <a-form-item label="管理员账号" name="adminAccount">
            <a-input v-model:value="formData.adminAccount" placeholder="登录账号，全局唯一" :maxlength="50" />
          </a-form-item>
          <a-form-item label="管理员姓名" name="adminName">
            <a-input v-model:value="formData.adminName" placeholder="姓名" :maxlength="50" />
          </a-form-item>
        </div>
        <a-form-item label="管理员手机（可选）" name="adminPhone">
          <a-input v-model:value="formData.adminPhone" placeholder="手机号" :maxlength="20" />
        </a-form-item>

        <div class="form-note">开通将自动建组织根 + 初始管理员（系统生成初始密码，仅展示一次）；租户为「试用」状态，正式开通请用「开通/续费」创建订阅。</div>
      </a-form>
      <template #footer>
        <div class="modal-footer">
          <a-button @click="dialogVisible = false">取消</a-button>
          <a-button type="primary" :loading="submitLoading" @click="handleSubmit">开通</a-button>
        </div>
      </template>
    </a-modal>

    <a-modal v-model:open="resultVisible" title="租户已开通" :width="480" :footer="null" :destroyOnClose="true">
      <div class="pr-note">初始密码仅展示一次，请立即安全交付管理员。</div>
      <div class="pr-row">
        <span class="pr-label">管理员账号</span>
        <span class="pr-value">{{ provisionResult?.adminAccount }}</span>
      </div>
      <div class="pr-row">
        <span class="pr-label">初始密码</span>
        <span class="pr-value pr-password">{{ provisionResult?.tempPassword }}</span>
        <a-button type="link" size="small" @click="copyPassword">复制</a-button>
      </div>
      <div class="modal-footer" style="margin-top: 16px">
        <a-button type="primary" @click="resultVisible = false">我已保存</a-button>
      </div>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { message, Modal } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import { PlusOutlined, ReloadOutlined, DownOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import DataTable from '@/components/DataTable.vue'
import StatusTag from '@/components/StatusTag.vue'
import {
  getTenants,
  createTenant,
  updateTenantStatus,
  getPlans,
  type TenantDto,
  type PlanDto,
  type ProvisionTenantResult,
} from '@/api/platform'
import {
  TENANT_STATUS_OPTIONS,
  ACCOUNT_SET_BIND_MODE_OPTIONS,
  TODO_CHANNEL_OPTIONS,
  ROOT_ORG_KIND_OPTIONS,
} from '@/types/platform'

const router = useRouter()

const navKey = ref('tenants')
const navOptions = [
  { label: '租户', value: 'tenants' },
  { label: '套餐', value: 'plans' },
  { label: '订阅', value: 'subscriptions' },
]
function onNav(v: string | number) {
  router.push(`/platform/${v}`)
}

const columns = [
  { title: '名称', dataIndex: 'name', key: 'name', width: 160 },
  { title: '编号', dataIndex: 'code', key: 'code', width: 140 },
  { title: '状态', dataIndex: 'status', key: 'status', width: 100, align: 'center' as const },
  { title: '账套模式', dataIndex: 'accountSetBindMode', key: 'accountSetBindMode', width: 110, align: 'center' as const },
  { title: '待办渠道', dataIndex: 'defaultTodoChannel', key: 'defaultTodoChannel', width: 100, align: 'center' as const },
  { title: '根组织', dataIndex: 'rootOrgId', key: 'rootOrgId', width: 90, align: 'center' as const },
  { title: '到期时间', dataIndex: 'expireAt', key: 'expireAt', width: 120, align: 'center' as const },
  { title: '操作', dataIndex: 'action', key: 'action', width: 200, align: 'center' as const, fixed: 'right' as const },
]

const loading = ref(false)
const rawData = ref<TenantDto[]>([])
const keyword = ref('')
const statusFilter = ref<number | undefined>(undefined)
const pagination = ref({ pageIndex: 1, pageSize: 20, total: 0 })

const displayData = computed(() => {
  const kw = keyword.value.trim().toLowerCase()
  return rawData.value.filter((t) => {
    const matchKw = !kw || t.name.toLowerCase().includes(kw) || t.code.toLowerCase().includes(kw)
    const matchStatus = statusFilter.value == null || t.status === statusFilter.value
    return matchKw && matchStatus
  })
})

// total 由过滤结果驱动（保持 computed 纯净，副作用移到 watch）
watch(displayData, (list) => { pagination.value.total = list.length }, { immediate: true })
// 过滤条件变化时回到第 1 页，避免客户端分页停在空白页
watch([keyword, statusFilter], () => { pagination.value.pageIndex = 1 })

const planOptions = ref<{ label: string; value: number }[]>([])

function labelOf(opts: { value: number; label: string }[], v: number) {
  return opts.find((o) => o.value === v)?.label ?? '-'
}

function statusType(status: number): 'success' | 'warning' | 'danger' | 'default' {
  if (status === 2) return 'success'
  if (status === 1) return 'warning'
  if (status === 4) return 'danger'
  return 'default'
}

async function fetchList() {
  loading.value = true
  try {
    const res = await getTenants()
    rawData.value = res || []
  } finally {
    loading.value = false
  }
}

async function fetchPlanOptions() {
  try {
    const res = await getPlans()
    planOptions.value = (res || []).map((p: PlanDto) => ({ label: `${p.name}（${p.code}）`, value: p.id }))
  } catch { planOptions.value = [] }
}

function changeStatus(record: TenantDto, target: number) {
  if (target === record.status) return
  const label = labelOf(TENANT_STATUS_OPTIONS, target)
  const destructive = target === 3 || target === 4
  const doUpdate = async () => {
    await updateTenantStatus(record.id, { status: target })
    message.success(`已设为${label}`)
    fetchList()
  }
  if (destructive) {
    Modal.confirm({
      title: `确定将租户「${record.name}」设为${label}吗？`,
      content: target === 4 ? '冻结后该租户的写操作将被拒绝（返回 402）。' : '停用后该租户将无法登录/解析。',
      okText: '确定',
      cancelText: '取消',
      okType: 'danger',
      onOk: doUpdate,
    })
  } else {
    doUpdate()
  }
}

function goSubscriptions(record: TenantDto) {
  router.push({ path: '/platform/subscriptions', query: { tenantId: String(record.id) } })
}

// ==================== 新建 ====================
const dialogVisible = ref(false)
const formRef = ref<FormInstance>()
const submitLoading = ref(false)
const formData = reactive({
  name: '',
  code: '',
  accountSetBindMode: 1,
  defaultTodoChannel: 1,
  planId: undefined as number | undefined,
  expireAt: undefined as string | undefined,
  rootOrgName: '',
  rootOrgKind: 1,
  adminAccount: '',
  adminName: '',
  adminPhone: '',
})
const formRules: Record<string, Rule[]> = {
  name: [{ required: true, message: '请输入租户名称', trigger: 'blur' }],
  code: [{ required: true, message: '请输入唯一编号', trigger: 'blur' }],
  rootOrgName: [{ required: true, message: '请输入根组织名称', trigger: 'blur' }],
  adminAccount: [{ required: true, message: '请输入管理员账号', trigger: 'blur' }],
  adminName: [{ required: true, message: '请输入管理员姓名', trigger: 'blur' }],
}

// 开通结果（临时密码一次性展示）
const resultVisible = ref(false)
const provisionResult = ref<ProvisionTenantResult | null>(null)
async function copyPassword() {
  if (!provisionResult.value) return
  try {
    await navigator.clipboard.writeText(provisionResult.value.tempPassword)
    message.success('已复制初始密码')
  } catch {
    message.warning('复制失败，请手动选择')
  }
}

function handleAdd() {
  formData.name = ''
  formData.code = ''
  formData.accountSetBindMode = 1
  formData.defaultTodoChannel = 1
  formData.planId = undefined
  formData.expireAt = undefined
  formData.rootOrgName = ''
  formData.rootOrgKind = 1
  formData.adminAccount = ''
  formData.adminName = ''
  formData.adminPhone = ''
  dialogVisible.value = true
}

async function handleSubmit() {
  if (!formRef.value) return
  try { await formRef.value.validate() } catch { return }
  submitLoading.value = true
  try {
    const result = await createTenant({
      name: formData.name,
      code: formData.code,
      accountSetBindMode: formData.accountSetBindMode,
      defaultTodoChannel: formData.defaultTodoChannel,
      planId: formData.planId ?? null,
      expireAt: formData.expireAt ?? null,
      rootOrgName: formData.rootOrgName,
      rootOrgKind: formData.rootOrgKind,
      adminAccount: formData.adminAccount,
      adminName: formData.adminName,
      adminPhone: formData.adminPhone || null,
    })
    dialogVisible.value = false
    provisionResult.value = result
    resultVisible.value = true
    message.success('租户已开通')
    fetchList()
  } finally {
    submitLoading.value = false
  }
}

onMounted(() => { fetchList(); fetchPlanOptions() })
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.form-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0 16px;
}

.form-section-title {
  margin: 4px 0 12px;
  font-size: $font-size-base;
  font-weight: 500;
  color: var(--text-2);
}

.form-hint {
  margin-top: 4px;
  font-size: $font-size-sm;
  color: $text-secondary;
}

.form-note {
  margin-top: 4px;
  padding: 8px 12px;
  border-radius: 8px;
  background: var(--color-info-light);
  color: var(--color-info-text);
  font-size: $font-size-sm;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}

.pr-note {
  padding: 8px 12px;
  margin-bottom: 12px;
  border-radius: 8px;
  background: var(--color-warning-light);
  color: var(--color-warning-text);
  font-size: $font-size-sm;
}

.pr-row {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 8px 0;
  border-bottom: 1px solid var(--border);
}

.pr-label {
  width: 84px;
  color: var(--text-3);
  font-size: $font-size-sm;
}

.pr-value {
  color: var(--text-1);
  font-size: $font-size-base;
}

.pr-password {
  font-family: var(--font-mono, monospace);
  letter-spacing: 1px;
}
</style>
