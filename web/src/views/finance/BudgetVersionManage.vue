<template>
  <div class="budget-page page-container">
    <PageHeader title="预算版本管理" description="按账套、年度和场景维护预算版本">
      <template #left>
        <AccountSetSelector style="width: 220px" />
        <a-input-number v-model:value="queryYear" :min="2000" :max="2100" size="small" style="width: 110px" @change="loadVersions" />
      </template>
      <template #actions>
        <a-button type="primary" @click="openCreateDialog">
          <template #icon><PlusOutlined /></template>
          新增版本
        </a-button>
      </template>
    </PageHeader>

    <div class="budget-metrics">
      <div class="metric-cell">
        <span>版本数</span>
        <strong>{{ versions.length }}</strong>
      </div>
      <div class="metric-cell">
        <span>已审批</span>
        <strong>{{ approvedCount }}</strong>
      </div>
      <div class="metric-cell">
        <span>草稿/提交中</span>
        <strong>{{ workingCount }}</strong>
      </div>
    </div>

    <a-card :bordered="false" class="table-card">
      <a-table
        :columns="columns"
        :data-source="versions"
        :loading="loading"
        row-key="id"
        bordered
        :pagination="false"
        :scroll="{ x: 1080 }"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'name'">
            <a @click="openEditor(record as BudgetVersionDto)">{{ record.name }}</a>
          </template>
          <template v-if="column.dataIndex === 'scenarioType'">
            {{ scenarioText(record.scenarioType) }}
          </template>
          <template v-if="column.dataIndex === 'status'">
            <a-tag :color="statusColor(record.status)">{{ statusText(record.status) }}</a-tag>
          </template>
          <template v-if="column.dataIndex === 'createdTime'">
            {{ formatDateTime(record.createdTime) }}
          </template>
          <template v-if="column.dataIndex === 'approvedTime'">
            {{ record.approvedTime ? formatDateTime(record.approvedTime) : '-' }}
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-space>
              <a-button type="link" size="small" @click="openEditor(record as BudgetVersionDto)">
                <EditOutlined />编制
              </a-button>
              <a-button v-if="record.status === 'draft'" type="link" size="small" @click="submitVersion(record as BudgetVersionDto)">
                <SendOutlined />提交
              </a-button>
              <a-button v-if="record.status === 'submitted'" type="link" size="small" @click="approveVersion(record as BudgetVersionDto)">
                <CheckOutlined />审批
              </a-button>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <a-modal v-model:open="createVisible" title="新增预算版本" :width="520" :destroy-on-close="true">
      <a-form ref="formRef" :model="form" :rules="rules" :label-col="{ style: { width: '96px' } }">
        <a-form-item label="版本名称" name="name">
          <a-input v-model:value="form.name" placeholder="例如：2026 年度预算" />
        </a-form-item>
        <a-form-item label="预算年度" name="year">
          <a-input-number v-model:value="form.year" :min="2000" :max="2100" style="width: 100%" />
        </a-form-item>
        <a-form-item label="预算场景" name="scenarioType">
          <a-select v-model:value="form.scenarioType" :options="scenarioOptions" />
        </a-form-item>
        <a-form-item label="归属组织" name="ownerOrgId">
          <a-input-number v-model:value="form.ownerOrgId" :min="0" style="width: 100%" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="createVisible = false">取消</a-button>
        <a-button type="primary" :loading="saving" @click="createVersion">保存</a-button>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import { CheckOutlined, EditOutlined, PlusOutlined, SendOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import AccountSetSelector from '@/components/AccountSetSelector.vue'
import {
  approveBudgetVersion,
  createBudgetVersion,
  getBudgetVersions,
  submitBudgetVersion,
  type BudgetVersionDto,
  type CreateBudgetVersionRequest,
} from '@/api/finance'
import { useAccountSetStore } from '@/stores/accountSet'
import { useOrgContextStore } from '@/stores/orgContext'

const router = useRouter()
const accountSetStore = useAccountSetStore()
const orgContextStore = useOrgContextStore()

const nowYear = new Date().getFullYear()
const queryYear = ref(nowYear)
const loading = ref(false)
const saving = ref(false)
const versions = ref<BudgetVersionDto[]>([])
const createVisible = ref(false)
const formRef = ref<FormInstance>()

const scenarioOptions = [
  { label: '年度预算', value: 'annual_budget' },
  { label: '滚动预测', value: 'rolling_forecast' },
  { label: '专项预算', value: 'special_budget' },
]

const form = reactive<CreateBudgetVersionRequest>({
  accountSetId: 0,
  name: '',
  scenarioType: 'annual_budget',
  year: nowYear,
  ownerOrgId: 0,
})

const rules: Record<string, Rule[]> = {
  name: [{ required: true, message: '请输入版本名称', trigger: 'blur' }],
  year: [{ required: true, message: '请输入预算年度', trigger: 'change' }],
  ownerOrgId: [{ required: true, message: '请输入归属组织', trigger: 'change' }],
}

const columns = [
  { title: '版本名称', dataIndex: 'name', key: 'name', width: 220 },
  { title: '场景', dataIndex: 'scenarioType', key: 'scenarioType', width: 120 },
  { title: '年度', dataIndex: 'year', key: 'year', width: 90, align: 'center' as const },
  { title: '组织ID', dataIndex: 'ownerOrgId', key: 'ownerOrgId', width: 100, align: 'center' as const },
  { title: '状态', dataIndex: 'status', key: 'status', width: 110, align: 'center' as const },
  { title: '创建人', dataIndex: 'createdBy', key: 'createdBy', width: 120 },
  { title: '创建时间', dataIndex: 'createdTime', key: 'createdTime', width: 170 },
  { title: '审批人', dataIndex: 'approvedBy', key: 'approvedBy', width: 120 },
  { title: '审批时间', dataIndex: 'approvedTime', key: 'approvedTime', width: 170 },
  { title: '操作', dataIndex: 'action', key: 'action', width: 210, align: 'center' as const, fixed: 'right' as const },
]

const approvedCount = computed(() => versions.value.filter((item) => item.status === 'approved').length)
const workingCount = computed(() => versions.value.filter((item) => item.status !== 'approved').length)

watch(
  () => accountSetStore.currentAccountSetId,
  () => loadVersions(),
)

onMounted(async () => {
  if (accountSetStore.accountSets.length === 0) {
    await accountSetStore.fetchAccountSets()
  }
  await loadVersions()
})

async function loadVersions() {
  const accountSetId = accountSetStore.getCurrentAccountSetId()
  if (!accountSetId) {
    versions.value = []
    return
  }

  loading.value = true
  try {
    versions.value = await getBudgetVersions({ accountSetId, year: queryYear.value })
  } catch {
    message.error('加载预算版本失败')
  } finally {
    loading.value = false
  }
}

function openCreateDialog() {
  const accountSetId = accountSetStore.getCurrentAccountSetId()
  if (!accountSetId) {
    message.warning('请先选择账套')
    return
  }

  Object.assign(form, {
    accountSetId,
    name: `${queryYear.value} 年度预算`,
    scenarioType: 'annual_budget',
    year: queryYear.value,
    ownerOrgId: orgContextStore.currentOrgId || 0,
  })
  createVisible.value = true
}

async function createVersion() {
  await formRef.value?.validate()
  saving.value = true
  try {
    const result = await createBudgetVersion({ ...form })
    message.success('预算版本已创建')
    createVisible.value = false
    await loadVersions()
    openEditor(result)
  } catch {
    message.error('创建预算版本失败')
  } finally {
    saving.value = false
  }
}

async function submitVersion(record: BudgetVersionDto) {
  await submitBudgetVersion(record.id)
  message.success('已提交预算版本')
  loadVersions()
}

async function approveVersion(record: BudgetVersionDto) {
  await approveBudgetVersion(record.id)
  message.success('已审批预算版本')
  loadVersions()
}

function openEditor(record: BudgetVersionDto) {
  router.push(`/finance/budget/editor/${record.id}`)
}

function scenarioText(value: string) {
  return scenarioOptions.find((item) => item.value === value)?.label || value
}

function statusText(value: string) {
  const map: Record<string, string> = {
    draft: '草稿',
    submitted: '已提交',
    approved: '已审批',
  }
  return map[value] || value
}

function statusColor(value: string) {
  const map: Record<string, string> = {
    draft: 'default',
    submitted: 'processing',
    approved: 'success',
  }
  return map[value] || 'default'
}

function formatDateTime(value: string) {
  if (!value) return '-'
  return value.replace('T', ' ').slice(0, 19)
}
</script>

<style scoped>
.budget-page {
  min-width: 0;
}

.budget-metrics {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 12px;
  margin-bottom: 12px;
}

.metric-cell {
  min-height: 70px;
  padding: 14px 16px;
  border: 1px solid #e7edf3;
  border-radius: 8px;
  background: #fff;
}

.metric-cell span {
  display: block;
  color: #64748b;
  font-size: 12px;
}

.metric-cell strong {
  display: block;
  margin-top: 8px;
  color: #1f2937;
  font-size: 24px;
  line-height: 1;
}
</style>
