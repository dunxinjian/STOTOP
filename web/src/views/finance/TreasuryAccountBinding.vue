<template>
  <div class="treasury-binding-page page-container">
    <PageHeader title="资金账户绑定" description="绑定资金渠道、现金科目和 13 周预测的期初现金口径">
      <template #left>
        <AccountSetSelector style="width: 220px" />
        <a-input-number v-model:value="filterOrgId" size="small" :min="0" placeholder="组织ID" style="width: 110px" @change="loadBindings" />
      </template>
      <template #actions>
        <a-button @click="addBinding">
          <template #icon><PlusOutlined /></template>
          新增绑定
        </a-button>
        <a-button @click="loadBindings">
          <template #icon><ReloadOutlined /></template>
          刷新
        </a-button>
      </template>
    </PageHeader>

    <a-card :bordered="false">
      <a-table
        :columns="columns"
        :data-source="rows"
        :loading="loading"
        row-key="clientKey"
        bordered
        :pagination="false"
        :scroll="{ x: 1160 }"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'orgId'">
            <a-input-number v-model:value="record.orgId" :min="0" size="small" style="width: 100%" placeholder="为空则全组织" />
          </template>
          <template v-if="column.dataIndex === 'channelId'">
            <a-select
              v-model:value="record.channelId"
              :options="channelOptions"
              size="small"
              allow-clear
              show-search
              option-filter-prop="label"
              style="width: 100%"
            />
          </template>
          <template v-if="column.dataIndex === 'cashAccountId'">
            <a-input-number v-model:value="record.cashAccountId" :min="0" size="small" style="width: 100%" placeholder="现金科目ID" />
          </template>
          <template v-if="column.dataIndex === 'accountNo'">
            <a-input v-model:value="record.accountNo" size="small" placeholder="银行账号/账户编号" />
          </template>
          <template v-if="column.dataIndex === 'openingSource'">
            <a-select v-model:value="record.openingSource" :options="openingSourceOptions" size="small" style="width: 100%" />
          </template>
          <template v-if="column.dataIndex === 'manualOpeningAmount'">
            <a-input-number v-model:value="record.manualOpeningAmount" :precision="2" size="small" style="width: 100%" />
          </template>
          <template v-if="column.dataIndex === 'status'">
            <a-switch v-model:checked="record.enabled" size="small" checked-children="启用" un-checked-children="停用" />
          </template>
          <template v-if="column.dataIndex === 'remark'">
            <a-input v-model:value="record.remark" size="small" placeholder="备注" />
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" :loading="record.saving" @click="saveBinding(record as BindingRow)">
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
  getAllEnabledBankChannels,
  getTreasuryAccountBindings,
  saveTreasuryAccountBinding,
  type TreasuryAccountBindingDto,
} from '@/api/finance'
import { useAccountSetStore } from '@/stores/accountSet'
import { useOrgContextStore } from '@/stores/orgContext'

interface BindingRow extends TreasuryAccountBindingDto {
  clientKey: string
  enabled: boolean
  saving?: boolean
}

const accountSetStore = useAccountSetStore()
const orgContextStore = useOrgContextStore()
const loading = ref(false)
const rows = ref<BindingRow[]>([])
const filterOrgId = ref<number | undefined>(undefined)
const channelOptions = ref<{ label: string; value: number }[]>([])

const openingSourceOptions = [
  { label: '科目余额', value: 'account_balance' },
  { label: '手工录入', value: 'manual' },
]

const columns = [
  { title: '组织ID', dataIndex: 'orgId', key: 'orgId', width: 120 },
  { title: '交易渠道', dataIndex: 'channelId', key: 'channelId', width: 180 },
  { title: '现金科目ID', dataIndex: 'cashAccountId', key: 'cashAccountId', width: 130 },
  { title: '账户编号', dataIndex: 'accountNo', key: 'accountNo', width: 180 },
  { title: '期初来源', dataIndex: 'openingSource', key: 'openingSource', width: 150 },
  { title: '手工期初', dataIndex: 'manualOpeningAmount', key: 'manualOpeningAmount', width: 150 },
  { title: '状态', dataIndex: 'status', key: 'status', width: 110, align: 'center' as const },
  { title: '备注', dataIndex: 'remark', key: 'remark', width: 220 },
  { title: '操作', dataIndex: 'action', key: 'action', width: 110, align: 'center' as const, fixed: 'right' as const },
]

watch(
  () => accountSetStore.currentAccountSetId,
  () => loadBindings(),
)

onMounted(async () => {
  if (accountSetStore.accountSets.length === 0) {
    await accountSetStore.fetchAccountSets()
  }
  await Promise.all([loadChannels(), loadBindings()])
})

async function loadChannels() {
  try {
    const channels = await getAllEnabledBankChannels() as any[]
    channelOptions.value = (channels || []).map((item) => ({
      label: `${item.name || item.channelName || '渠道'}${item.accountNo ? ` ${item.accountNo}` : ''}`,
      value: item.id,
    }))
  } catch {
    channelOptions.value = []
  }
}

async function loadBindings() {
  const accountSetId = accountSetStore.getCurrentAccountSetId()
  if (!accountSetId) {
    rows.value = []
    return
  }

  loading.value = true
  try {
    const result = await getTreasuryAccountBindings({ accountSetId, orgId: filterOrgId.value })
    rows.value = result.map(toRow)
  } catch {
    message.error('加载资金账户绑定失败')
  } finally {
    loading.value = false
  }
}

function addBinding() {
  const accountSetId = accountSetStore.getCurrentAccountSetId()
  if (!accountSetId) {
    message.warning('请先选择账套')
    return
  }

  rows.value.unshift(toRow({
    accountSetId,
    orgId: filterOrgId.value ?? orgContextStore.currentOrgId ?? null,
    channelId: null,
    cashAccountId: null,
    accountNo: '',
    openingSource: 'account_balance',
    manualOpeningAmount: null,
    status: 1,
    remark: '',
  }))
}

async function saveBinding(record: BindingRow) {
  if (!record.channelId && !record.cashAccountId && !record.accountNo) {
    message.warning('请至少填写交易渠道、现金科目或账户编号')
    return
  }

  record.saving = true
  try {
    const { clientKey, enabled, saving, ...payload } = record
    await saveTreasuryAccountBinding({
      ...payload,
      status: enabled ? 1 : 0,
      orgId: payload.orgId || null,
      channelId: payload.channelId || null,
      cashAccountId: payload.cashAccountId || null,
      manualOpeningAmount: payload.manualOpeningAmount || null,
    })
    message.success('资金账户绑定已保存')
    await loadBindings()
  } catch {
    message.error('保存资金账户绑定失败')
  } finally {
    record.saving = false
  }
}

function toRow(item: TreasuryAccountBindingDto): BindingRow {
  return {
    ...item,
    clientKey: `${item.id || 'new'}-${Math.random().toString(36).slice(2)}`,
    enabled: item.status !== 0,
  }
}
</script>
