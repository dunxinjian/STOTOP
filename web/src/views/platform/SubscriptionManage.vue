<template>
  <div class="page-container">
    <PageHeader title="订阅续费" description="新建订阅即为租户开通/续费并激活">
      <template #left>
        <a-segmented v-model:value="navKey" :options="navOptions" @change="onNav" />
        <a-select
          v-model:value="filterTenantId"
          placeholder="全部租户"
          allow-clear
          show-search
          option-filter-prop="label"
          size="middle"
          style="width: 200px"
          :options="tenantOptions"
          @change="fetchList"
        />
        <a-button size="middle" @click="fetchList">
          <template #icon><ReloadOutlined /></template>刷新
        </a-button>
      </template>
      <template #right>
        <a-button type="primary" size="middle" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新建订阅
        </a-button>
      </template>
    </PageHeader>

    <div class="page-card">
      <DataTable
        v-model:pagination="pagination"
        :columns="columns"
        :data-source="tableData"
        :loading="loading"
        :scroll="{ x: 900 }"
        row-key="id"
        empty-text="暂无订阅数据"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'tenantId'">
            {{ tenantName(record.tenantId) }}
          </template>
          <template v-if="column.dataIndex === 'planId'">
            {{ planName(record.planId) }}
          </template>
          <template v-if="column.dataIndex === 'periodStart'">
            {{ String(record.periodStart).slice(0, 10) }}
          </template>
          <template v-if="column.dataIndex === 'periodEnd'">
            {{ String(record.periodEnd).slice(0, 10) }}
          </template>
        </template>
      </DataTable>
    </div>

    <a-modal
      v-model:open="dialogVisible"
      title="新建订阅（开通 / 续费）"
      :width="560"
      :destroyOnClose="true"
    >
      <a-form ref="formRef" :model="formData" :rules="formRules" layout="vertical" class="modal-form">
        <a-form-item label="租户" name="tenantId">
          <a-select
            v-model:value="formData.tenantId"
            placeholder="请选择租户"
            show-search
            option-filter-prop="label"
            :options="tenantOptions"
          />
        </a-form-item>
        <a-form-item label="套餐" name="planId">
          <a-select
            v-model:value="formData.planId"
            placeholder="请选择套餐"
            show-search
            option-filter-prop="label"
            :options="planOptions"
          />
        </a-form-item>
        <a-form-item label="订阅周期" name="period">
          <a-range-picker
            v-model:value="formData.period"
            valueFormat="YYYY-MM-DD"
            style="width: 100%"
          />
        </a-form-item>
        <div class="form-note">提交后该租户将转为「正式」，并回填开通时间与到期时间（= 周期止）。</div>
      </a-form>
      <template #footer>
        <div class="modal-footer">
          <a-button @click="dialogVisible = false">取消</a-button>
          <a-button type="primary" :loading="submitLoading" @click="handleSubmit">确定</a-button>
        </div>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import { PlusOutlined, ReloadOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import DataTable from '@/components/DataTable.vue'
import {
  getSubscriptions,
  createSubscription,
  getTenants,
  getPlans,
  type SubscriptionDto,
  type TenantDto,
  type PlanDto,
} from '@/api/platform'

const route = useRoute()
const router = useRouter()

const navKey = ref('subscriptions')
const navOptions = [
  { label: '租户', value: 'tenants' },
  { label: '套餐', value: 'plans' },
  { label: '订阅', value: 'subscriptions' },
]
function onNav(v: string | number) {
  router.push(`/platform/${v}`)
}

const columns = [
  { title: '订阅ID', dataIndex: 'id', key: 'id', width: 90, align: 'center' as const },
  { title: '租户', dataIndex: 'tenantId', key: 'tenantId', width: 180 },
  { title: '套餐', dataIndex: 'planId', key: 'planId', width: 180 },
  { title: '周期起', dataIndex: 'periodStart', key: 'periodStart', width: 120, align: 'center' as const },
  { title: '周期止', dataIndex: 'periodEnd', key: 'periodEnd', width: 120, align: 'center' as const },
]

const loading = ref(false)
const tableData = ref<SubscriptionDto[]>([])
const pagination = ref({ pageIndex: 1, pageSize: 20, total: 0 })
const filterTenantId = ref<number | undefined>(undefined)

const tenants = ref<TenantDto[]>([])
const plans = ref<PlanDto[]>([])
const tenantOptions = ref<{ label: string; value: number }[]>([])
const planOptions = ref<{ label: string; value: number }[]>([])

function tenantName(id: number) {
  return tenants.value.find((t) => t.id === id)?.name ?? `#${id}`
}
function planName(id: number) {
  const p = plans.value.find((x) => x.id === id)
  return p ? `${p.name}（${p.code}）` : `#${id}`
}

async function fetchList() {
  loading.value = true
  try {
    const res = await getSubscriptions(filterTenantId.value)
    tableData.value = res || []
    pagination.value.total = tableData.value.length
  } finally {
    loading.value = false
  }
}

async function fetchRefs() {
  try {
    const [ts, ps] = await Promise.all([getTenants(), getPlans()])
    tenants.value = ts || []
    plans.value = ps || []
    tenantOptions.value = tenants.value.map((t) => ({ label: `${t.name}（${t.code}）`, value: t.id }))
    planOptions.value = plans.value.map((p) => ({ label: `${p.name}（${p.code}）`, value: p.id }))
  } catch { /* 静默，列表仍可用 id 兜底显示 */ }
}

// ==================== 新建 ====================
const dialogVisible = ref(false)
const formRef = ref<FormInstance>()
const submitLoading = ref(false)
const formData = reactive({
  tenantId: undefined as number | undefined,
  planId: undefined as number | undefined,
  period: undefined as [string, string] | undefined,
})
const formRules: Record<string, Rule[]> = {
  tenantId: [{ required: true, message: '请选择租户', trigger: 'change' }],
  planId: [{ required: true, message: '请选择套餐', trigger: 'change' }],
  period: [{ required: true, message: '请选择订阅周期', trigger: 'change', type: 'array' }],
}

function handleAdd() {
  formData.tenantId = filterTenantId.value
  formData.planId = undefined
  formData.period = undefined
  dialogVisible.value = true
}

async function handleSubmit() {
  if (!formRef.value) return
  try { await formRef.value.validate() } catch { return }
  if (!formData.period || formData.period.length !== 2) { message.warning('请选择订阅周期'); return }
  submitLoading.value = true
  try {
    await createSubscription({
      tenantId: formData.tenantId!,
      planId: formData.planId!,
      periodStart: formData.period[0],
      periodEnd: formData.period[1],
    })
    message.success('订阅已创建，租户已激活')
    dialogVisible.value = false
    fetchList()
  } finally {
    submitLoading.value = false
  }
}

onMounted(async () => {
  const q = route.query.tenantId
  if (typeof q === 'string' && q) filterTenantId.value = Number(q)
  await fetchRefs()
  await fetchList()
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

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
</style>
