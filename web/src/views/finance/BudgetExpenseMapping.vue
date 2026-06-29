<template>
  <div class="budget-mapping-page page-container">
    <PageHeader title="费用预算映射" description="维护费用类型、会计科目、损益项和资金分类之间的控制关系">
      <template #left>
        <AccountSetSelector style="width: 220px" />
        <a-input-number v-model:value="filterOrgId" size="small" :min="0" placeholder="组织ID" style="width: 110px" @change="loadMappings" />
      </template>
      <template #actions>
        <a-button @click="addMapping">
          <template #icon><PlusOutlined /></template>
          新增映射
        </a-button>
        <a-button @click="loadMappings">
          <template #icon><ReloadOutlined /></template>
          刷新
        </a-button>
      </template>
    </PageHeader>

    <a-alert
      class="mapping-alert"
      type="info"
      show-icon
      message="费用类型映射用于预算预览、预算占用和 13 周资金预测的现金分类口径。"
    />

    <a-card :bordered="false">
      <a-table
        :columns="columns"
        :data-source="rows"
        :loading="loading"
        row-key="clientKey"
        bordered
        :pagination="false"
        :scroll="{ x: 1100 }"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'expenseType'">
            <a-input v-model:value="record.expenseType" size="small" placeholder="如：差旅费" />
          </template>
          <template v-if="column.dataIndex === 'orgId'">
            <a-input-number v-model:value="record.orgId" :min="0" size="small" style="width: 100%" placeholder="为空则全组织" />
          </template>
          <template v-if="column.dataIndex === 'accountCode'">
            <a-input v-model:value="record.accountCode" size="small" placeholder="科目编码" />
          </template>
          <template v-if="column.dataIndex === 'plItemId'">
            <a-input-number v-model:value="record.plItemId" :min="0" size="small" style="width: 100%" />
          </template>
          <template v-if="column.dataIndex === 'cashCategory'">
            <a-select v-model:value="record.cashCategory" size="small" :options="cashCategoryOptions" style="width: 100%" />
          </template>
          <template v-if="column.dataIndex === 'status'">
            <a-switch v-model:checked="record.enabled" size="small" checked-children="启用" un-checked-children="停用" />
          </template>
          <template v-if="column.dataIndex === 'remark'">
            <a-input v-model:value="record.remark" size="small" placeholder="备注" />
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" :loading="record.saving" @click="saveMapping(record as MappingRow)">
              <SaveOutlined />保存
            </a-button>
          </template>
        </template>
      </a-table>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { message } from 'ant-design-vue'
import { PlusOutlined, ReloadOutlined, SaveOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import AccountSetSelector from '@/components/AccountSetSelector.vue'
import {
  getBudgetExpenseMappings,
  saveBudgetExpenseMapping,
  type BudgetExpenseMappingDto,
} from '@/api/finance'
import { useAccountSetStore } from '@/stores/accountSet'
import { useOrgContextStore } from '@/stores/orgContext'

interface MappingRow extends BudgetExpenseMappingDto {
  clientKey: string
  enabled: boolean
  saving?: boolean
}

const accountSetStore = useAccountSetStore()
const orgContextStore = useOrgContextStore()
const loading = ref(false)
const rows = ref<MappingRow[]>([])
const filterOrgId = ref<number | undefined>(undefined)

const cashCategoryOptions = [
  { label: '费用报销', value: 'expense_reimbursement' },
  { label: '采购付款', value: 'purchase_payment' },
  { label: '工资社保', value: 'payroll' },
  { label: '税费', value: 'tax' },
  { label: '其他支出', value: 'other_outflow' },
]

const columns = [
  { title: '费用类型', dataIndex: 'expenseType', key: 'expenseType', width: 160 },
  { title: '组织ID', dataIndex: 'orgId', key: 'orgId', width: 120 },
  { title: '科目编码', dataIndex: 'accountCode', key: 'accountCode', width: 140 },
  { title: '损益项ID', dataIndex: 'plItemId', key: 'plItemId', width: 120 },
  { title: '资金分类', dataIndex: 'cashCategory', key: 'cashCategory', width: 170 },
  { title: '状态', dataIndex: 'status', key: 'status', width: 110, align: 'center' as const },
  { title: '备注', dataIndex: 'remark', key: 'remark', width: 220 },
  { title: '操作', dataIndex: 'action', key: 'action', width: 110, align: 'center' as const, fixed: 'right' as const },
]

watch(
  () => accountSetStore.currentAccountSetId,
  () => loadMappings(),
)

onMounted(async () => {
  if (accountSetStore.accountSets.length === 0) {
    await accountSetStore.fetchAccountSets()
  }
  await loadMappings()
})

async function loadMappings() {
  const accountSetId = accountSetStore.getCurrentAccountSetId()
  if (!accountSetId) {
    rows.value = []
    return
  }

  loading.value = true
  try {
    const result = await getBudgetExpenseMappings({ accountSetId, orgId: filterOrgId.value })
    rows.value = result.map(toRow)
  } catch {
    message.error('加载费用映射失败')
  } finally {
    loading.value = false
  }
}

function addMapping() {
  const accountSetId = accountSetStore.getCurrentAccountSetId()
  if (!accountSetId) {
    message.warning('请先选择账套')
    return
  }

  rows.value.unshift(toRow({
    accountSetId,
    orgId: filterOrgId.value ?? orgContextStore.currentOrgId ?? null,
    expenseType: '',
    accountCode: '',
    plItemId: null,
    cashCategory: 'expense_reimbursement',
    status: 1,
    remark: '',
  }))
}

async function saveMapping(record: MappingRow) {
  if (!record.expenseType?.trim()) {
    message.warning('请输入费用类型')
    return
  }

  record.saving = true
  try {
    const { clientKey, enabled, saving, ...payload } = record
    await saveBudgetExpenseMapping({
      ...payload,
      status: enabled ? 1 : 0,
      orgId: payload.orgId || null,
      accountCode: payload.accountCode || null,
      plItemId: payload.plItemId || null,
    })
    message.success('费用映射已保存')
    await loadMappings()
  } catch {
    message.error('保存费用映射失败')
  } finally {
    record.saving = false
  }
}

function toRow(item: BudgetExpenseMappingDto): MappingRow {
  return {
    ...item,
    clientKey: `${item.id || 'new'}-${Math.random().toString(36).slice(2)}`,
    enabled: item.status !== 0,
  }
}
</script>

<style scoped>
.mapping-alert {
  margin-bottom: 12px;
}
</style>
