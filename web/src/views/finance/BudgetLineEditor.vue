<template>
  <div class="budget-line-page page-container">
    <PageHeader :title="`预算编制 #${versionId}`" description="按期间、组织、科目或损益项维护预算金额">
      <template #left>
        <AccountSetSelector style="width: 220px" />
        <a-input v-model:value="filters.period" size="small" placeholder="期间 YYYYMM" style="width: 120px" @pressEnter="loadLines" />
        <a-input-number v-model:value="filters.orgId" size="small" :min="0" placeholder="组织ID" style="width: 110px" @change="loadLines" />
      </template>
      <template #actions>
        <a-button @click="router.back()">
          <template #icon><ArrowLeftOutlined /></template>
          返回
        </a-button>
        <a-button @click="addLine">
          <template #icon><PlusOutlined /></template>
          新增行
        </a-button>
        <a-button type="primary" :loading="saving" @click="saveLines">
          <template #icon><SaveOutlined /></template>
          保存
        </a-button>
      </template>
    </PageHeader>

    <a-card :bordered="false" class="editor-card">
      <a-table
        :columns="columns"
        :data-source="rows"
        :loading="loading"
        row-key="clientKey"
        bordered
        :pagination="false"
        :scroll="{ x: 1220 }"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'period'">
            <a-input v-model:value="record.period" size="small" placeholder="YYYYMM" />
          </template>
          <template v-if="column.dataIndex === 'orgId'">
            <a-input-number v-model:value="record.orgId" :min="0" size="small" style="width: 100%" />
          </template>
          <template v-if="column.dataIndex === 'accountCode'">
            <a-input v-model:value="record.accountCode" size="small" placeholder="科目编码" />
          </template>
          <template v-if="column.dataIndex === 'plItemId'">
            <a-input-number v-model:value="record.plItemId" :min="0" size="small" style="width: 100%" />
          </template>
          <template v-if="column.dataIndex === 'amoebaUnitId'">
            <a-input-number v-model:value="record.amoebaUnitId" :min="0" size="small" style="width: 100%" />
          </template>
          <template v-if="column.dataIndex === 'quantity'">
            <a-input-number v-model:value="record.quantity" :precision="2" size="small" style="width: 100%" />
          </template>
          <template v-if="column.dataIndex === 'unitPrice'">
            <a-input-number v-model:value="record.unitPrice" :precision="2" size="small" style="width: 100%" />
          </template>
          <template v-if="column.dataIndex === 'amount'">
            <a-input-number v-model:value="record.amount" :precision="2" size="small" style="width: 100%" />
          </template>
          <template v-if="column.dataIndex === 'remark'">
            <a-input v-model:value="record.remark" size="small" placeholder="备注" />
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" danger @click="removeLine(record.clientKey)">
              <DeleteOutlined />移除
            </a-button>
          </template>
        </template>
        <template #emptyText>
          <div class="empty-editor">
            <span>暂无预算明细</span>
            <a-button type="link" @click="addLine">新增第一行</a-button>
          </div>
        </template>
      </a-table>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import { ArrowLeftOutlined, DeleteOutlined, PlusOutlined, SaveOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import AccountSetSelector from '@/components/AccountSetSelector.vue'
import { batchUpsertBudgetLines, getBudgetLines, type BudgetLineDto } from '@/api/finance'
import { useAccountSetStore } from '@/stores/accountSet'
import { useOrgContextStore } from '@/stores/orgContext'

interface BudgetLineRow extends BudgetLineDto {
  clientKey: string
}

const route = useRoute()
const router = useRouter()
const accountSetStore = useAccountSetStore()
const orgContextStore = useOrgContextStore()

const versionId = computed(() => Number(route.params.versionId || 0))
const loading = ref(false)
const saving = ref(false)
const rows = ref<BudgetLineRow[]>([])

const filters = reactive({
  period: currentPeriod(),
  orgId: orgContextStore.currentOrgId || 0,
})

const columns = [
  { title: '期间', dataIndex: 'period', key: 'period', width: 110 },
  { title: '组织ID', dataIndex: 'orgId', key: 'orgId', width: 100 },
  { title: '科目编码', dataIndex: 'accountCode', key: 'accountCode', width: 140 },
  { title: '损益项ID', dataIndex: 'plItemId', key: 'plItemId', width: 110 },
  { title: '阿米巴ID', dataIndex: 'amoebaUnitId', key: 'amoebaUnitId', width: 110 },
  { title: '数量', dataIndex: 'quantity', key: 'quantity', width: 120 },
  { title: '单价', dataIndex: 'unitPrice', key: 'unitPrice', width: 120 },
  { title: '预算金额', dataIndex: 'amount', key: 'amount', width: 140 },
  { title: '备注', dataIndex: 'remark', key: 'remark', width: 220 },
  { title: '操作', dataIndex: 'action', key: 'action', width: 90, align: 'center' as const, fixed: 'right' as const },
]

watch(
  () => accountSetStore.currentAccountSetId,
  () => loadLines(),
)

onMounted(async () => {
  if (accountSetStore.accountSets.length === 0) {
    await accountSetStore.fetchAccountSets()
  }
  await loadLines()
})

async function loadLines() {
  if (!versionId.value) return
  loading.value = true
  try {
    const result = await getBudgetLines(versionId.value, {
      period: filters.period || undefined,
      orgId: filters.orgId || undefined,
    })
    rows.value = result.map(toRow)
  } catch {
    message.error('加载预算明细失败')
  } finally {
    loading.value = false
  }
}

function addLine() {
  rows.value.push(toRow({
    budgetVersionId: versionId.value,
    period: filters.period || currentPeriod(),
    orgId: filters.orgId || orgContextStore.currentOrgId || 0,
    accountCode: '',
    plItemId: null,
    amoebaUnitId: null,
    amount: 0,
    quantity: null,
    unitPrice: null,
    remark: '',
  }))
}

function removeLine(key: string) {
  rows.value = rows.value.filter((row) => row.clientKey !== key)
}

async function saveLines() {
  if (!versionId.value) return
  const invalid = rows.value.find((row) => !row.period || !row.orgId || Number(row.amount || 0) === 0)
  if (invalid) {
    message.warning('请补齐期间、组织和预算金额')
    return
  }

  saving.value = true
  try {
    await batchUpsertBudgetLines(versionId.value, rows.value.map((row) => {
      const { clientKey, ...line } = row
      return line
    }))
    message.success('预算明细已保存')
    await loadLines()
  } catch {
    message.error('保存预算明细失败')
  } finally {
    saving.value = false
  }
}

function toRow(line: BudgetLineDto): BudgetLineRow {
  return {
    ...line,
    amount: Number(line.amount || 0),
    clientKey: `${line.id || 'new'}-${Math.random().toString(36).slice(2)}`,
  }
}

function currentPeriod() {
  const now = new Date()
  return `${now.getFullYear()}${String(now.getMonth() + 1).padStart(2, '0')}`
}
</script>

<style scoped>
.editor-card {
  min-width: 0;
}

.empty-editor {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  min-height: 120px;
  color: #6b7280;
}
</style>
